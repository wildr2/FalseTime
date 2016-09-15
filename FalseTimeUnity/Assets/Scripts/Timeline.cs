using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class Timeline : MonoBehaviour
{
    public int LineID { get; private set; }

    // Debug
    public bool log_states = false;

    // References
    private GameManager gm;
    private CamController cam;

    // UI
    private bool focused = true;
    public RectTransform cmds_parent;
    public RectTransform line, knob, marker_prefab;
    private bool pointer_down;
    public Text clock, win_text, score_text, score_marker_prefab;

    // Interaction
    private bool paused = true;

    // Time
    public float Time { get; private set; }
    private float time_length = 120;

    // History
    private WorldState state_0;
    private LinkedList<WorldState> key_states;
    private LinkedList<PlayerCmd> player_cmds;
    private int latest_cmd_id = -1;
    private int last_drawn_cmd_id = -1;
    private float latest_cmd_time;

    // Events
    public System.Action<Timeline> on_time_set;
    public System.Action<Timeline> on_history_change;


    // PUBLIC ACCESSORS

    public float GetEndTime()
    {
        return time_length;
    }
    public WorldState GetState(float time)
    {
        WorldState recent = GetMostRecentKeyState(time).Value;
        WorldState newstate = new WorldState(recent);
        newstate.time = time;

        // Interpolate
        float time_since = time - recent.time;

        for (int i = 0; i < gm.planets.Length; ++i)
        {
            int growth = Mathf.FloorToInt(gm.planets[i].GetPopPerSecond(newstate.planet_ownerIDs[i]) * time_since);
            newstate.planet_pops[i] += growth;
        }

        return newstate;
    }
    public int GetStateWinner(float time)
    {
        WorldState state = GetState(time);
        HashSet<int> alive_players = new HashSet<int>();

        for (int i = 0; i < gm.planets.Length; ++i)
        {
            if (state.planet_ownerIDs[i] >= 0)
                alive_players.Add(state.planet_ownerIDs[i]);
        }

        if (alive_players.Count == 1)
        {
            // Only one player alive - the winner
            foreach (int i in alive_players)
                return i; // HACKY

            return alive_players.GetEnumerator().Current;
        }

        // No winner
        return -1;
    }
    public float GetLatestCmdTime()
    {
        return latest_cmd_time;
    }

    public LinkedList<WorldState> GetKeyStates()
    {
        return key_states;
    }
    public LinkedList<PlayerCmd> GetPlayerCmds()
    {
        return player_cmds;
    }


    // PUBLIC MODIFIERS

    public void Initialize(int id)
    {
        LineID = id;

        // Create initial history
        state_0 = new WorldState(0, gm.planets);

        // Rotate starting planets
        for (int i = 0; i < state_0.planet_ownerIDs.Length; ++i)
        {
            if (state_0.planet_ownerIDs[i] != -1)
            {
                state_0.planet_ownerIDs[i] = (state_0.planet_ownerIDs[i] + id)
                    % gm.GetNumPlayers();
            }
        }


        RemakeKeyStates();
    }
    public void SetTime(float time)
    {
        Time = time;

        // Load state
        LoadState(GetState(time));

        // UI
        SetMarkerPosition(knob, time);
        UpdateClock();

        // Event
        if (on_time_set != null)
            on_time_set(this);
    }

    public void OnPointerDown()
    {
        pointer_down = true;
        if (cam != null) cam.EnableTranslation(false);
    }
    public void OnPointerUp()
    {
        pointer_down = false;
        if (cam != null) cam.EnableTranslation(true);
    }

    public void AddPlayerCmd(PlayerCmd cmd)
    {
        cmd.cmd_id = ++latest_cmd_id;
        latest_cmd_time = cmd.time;
        SaveCommand(cmd);
        RemakeKeyStates();
        if (gm.CurrentTimeline == this) LoadState(GetState(Time));
    }


    // PRIVATE MODIFIERS

    private void Awake()
    {
        cam = Camera.main.GetComponent<CamController>();

        // Events
        gm = FindObjectOfType<GameManager>();
        gm.on_win += OnWin;
        gm.on_time_set += OnTimeSet;

        // UI
        SetMarkerPosition(knob, 0);
        UpdateClock();

        // Player commands
        player_cmds = new LinkedList<PlayerCmd>();

        // Key states
        key_states = new LinkedList<WorldState>();
    }
    private void Update()
    {
        //if (!gm.IsGamePlaying()) return;

        if (pointer_down)
        {
            // Set Time (and update ui)
            float x = Input.mousePosition.x;
            SetTime(TimeFromMousePos(x));
        }

        // Play / Pause
        if (Input.GetKeyDown(KeyCode.Space))
            paused = !paused;

        if (!paused)
        {
            SetTime(Time + UnityEngine.Time.deltaTime);
            SetMarkerPosition(knob, Time);
        }
    }

    // General World State
    private void LoadState(WorldState state)
    {
        // Update gm.planets
        for (int i = 0; i < gm.planets.Length; ++i)
        {
            gm.planets[i].SetPop(state.planet_pops[i], state.planet_ownerIDs[i]);
        }

        // Destroy existing gm.fleets
        foreach (Fleet fleet in gm.fleets)
        {
            Destroy(fleet.gameObject);
        }
        gm.fleets.Clear();

        // Add new gm.fleets
        foreach (Flight flight in state.flights)
        {
            Fleet fleet = Instantiate(gm.fleet_prefab);
            fleet.Initialize(flight.owner_id, flight.ships, gm.player_colors[flight.owner_id]);
            fleet.SetPosition(gm.planets[flight.start_planet_id], gm.planets[flight.end_planet_id], flight.GetProgress(state.time));
            gm.fleets.Add(fleet);
        }
    }

    // Key States
    private void SaveKeyState(WorldState state)
    {
        LinkedListNode<WorldState> recent = GetMostRecentKeyState(state.time);

        if (recent == null)
        {
            // Add earliest state
            key_states.AddFirst(state);
        }
        else if (recent.Value.time == state.time)
        {
            // Replace existing state
            recent.Value = state;
        }
        else
        {
            // Add new state
            key_states.AddAfter(recent, state);
        }
    }
    private LinkedListNode<WorldState> GetMostRecentKeyState(float time)
    {
        if (key_states.Count == 0) return null;

        // Find most recent (to time) state
        LinkedListNode<WorldState> node = key_states.Last;
        while (node != null && node.Value.time > time)
        {
            node = node.Previous;
        }
        return node;
    }
    private void RemakeKeyStates()
    {
        RemakeKeyStates(new SortedList<float, TLEvent>(new DuplicateKeyComparer<float>()));
    }
    private void RemakeKeyStates(SortedList<float, TLEvent> key_events)
    {
        bool pass2 = key_events.Count > 0;

        // Delete old key states
        key_states.Clear();

        // Add back time 0 state
        SaveKeyState(state_0);

        // Create other key states
        foreach (PlayerCmd cmd in player_cmds) key_events.Add(cmd.time, new TLECmd(cmd));
        SortedList<float, TLEvent> next_pass_key_events = new SortedList<float, TLEvent>(new DuplicateKeyComparer<float>());

        while (key_events.Count > 0)
        {
            TLEvent e = key_events.Values[0];
            float time = key_events.Keys[0];
            key_events.RemoveAt(0);

            if (e as TLECmd != null)
            {
                // Player command
                WorldState state = GetState(e.cmd.time);
                Flight new_flight = e.cmd.TryToApply(state, gm.planets, gm.planet_routes);
                if (new_flight != null)
                {
                    // Command is valid in current history
                    e.cmd.valid = true;
                    if (new_flight.end_time > e.cmd.time)
                    {
                        key_events.Add(new_flight.end_time, new TLEFlightEnd(new_flight, e.cmd));
                    }
                    else // flight to past
                    {
                        if (!pass2) next_pass_key_events.Add(new_flight.start_time, new TLEFlightStart(new_flight, e.cmd));
                    }
                    SaveKeyState(state);
                }
                else
                {
                    e.cmd.valid = false;
                }
            }
            else if (e as TLEFlightStart != null)
            {
                // Flight start (from command at other time)
                WorldState state = GetState(e.flight.start_time);
                state.flights.Add(e.flight);
                key_events.Add(e.flight.end_time, new TLEFlightEnd(e.flight, e.cmd));
                SaveKeyState(state);
            }
            else if (e as TLEFlightEnd != null)
            {
                // Flight end
                if (e.flight.end_time <= GetEndTime())
                {
                    WorldState state = GetState(e.flight.end_time);
                    bool scored = false;
                    ApplyFlightEnd(state, e.flight, out scored);
                    SaveKeyState(state);

                    if (scored && !e.cmd.scored)
                    {
                        gm.GivePoint(e.cmd.player_id);
                        e.cmd.scored = true;
                        e.cmd.score_time = e.flight.end_time;
                    }
                }
            }
        }

        // Next pass
        if (next_pass_key_events.Count > 0)
        {
            RemakeKeyStates(next_pass_key_events);
            return;
        }

        // Event
        OnHistoryChange();

        // Debug
        if (log_states) LogStates();
    }
    private void ApplyFlightEnd(WorldState state, Flight flight, out bool scored)
    {
        scored = false;
        state.flights.Remove(flight);

        if (flight.owner_id == state.planet_ownerIDs[flight.end_planet_id])
        {
            // Transfer
            state.planet_pops[flight.end_planet_id] += flight.ships;
        }
        else
        {
            // Attack
            int new_pop = state.planet_pops[flight.end_planet_id] - flight.ships;
            if (new_pop >= 0) state.planet_pops[flight.end_planet_id] = new_pop;
            else
            {
                // Allegiance change
                new_pop = state.planet_pops[flight.end_planet_id] + flight.ships;
                if (state.planet_ownerIDs[flight.end_planet_id] != -1) scored = true;
                state.planet_pops[flight.end_planet_id] = new_pop;
                state.planet_ownerIDs[flight.end_planet_id] = flight.owner_id;
            }
        }
    }


    // Player Commands
    private void SaveCommand(PlayerCmd cmd)
    {
        LinkedListNode<PlayerCmd> recent = GetMostRecentCmd(cmd.time);

        if (recent == null)
        {
            // Add earliest command
            player_cmds.AddFirst(cmd);
        }
        else
        {
            // Add new command
            player_cmds.AddAfter(recent, cmd);
        }
    }
    private LinkedListNode<PlayerCmd> GetMostRecentCmd(float time)
    {
        if (player_cmds.Count == 0) return null;

        // Find most recent (to time) command
        LinkedListNode<PlayerCmd> node = player_cmds.Last;
        while (node != null && node.Value.time > time)
        {
            node = node.Previous;
        }
        return node;
    }

    // Events
    private void OnWin(int winner, float win_time)
    {
    }
    private void OnHistoryChange()
    {
        UpdateHistoryMarkers();
        if (on_history_change != null) on_history_change(this);
    }
    private void OnTimeSet(Timeline line)
    {
        if (line == this && !focused)
        {
            // Gain Focus
            focused = true;
            knob.GetComponent<Image>().color = Color.white;
            clock.color = Color.white;
        }
        else if (line != this && focused)
        {
            // Lose Focus
            focused = false;
            knob.GetComponent<Image>().color = Color.Lerp(Color.white, Color.black, 0.5f);
            clock.color = Color.Lerp(Color.white, Color.black, 0.5f);
        }
    }

    // Interaction and UI
    private void UpdateHistoryMarkers()
    {
        // Delete old markers
        Tools.DestroyChildren(cmds_parent);

        // Commands
        float prev_marker_time = -1;
        foreach (PlayerCmd cmd in GetPlayerCmds())
        {
            // Determine marker time to avoid overlapping markers
            float marker_time = cmd.time;
            if (marker_time - prev_marker_time < 0.1f)
            {
                marker_time = prev_marker_time + 0.1f;
            }
            prev_marker_time = marker_time;

            // Create and position marker
            RectTransform marker = Instantiate(marker_prefab);
            marker.SetParent(cmds_parent, false);
            SetMarkerPosition(marker, marker_time);

            // Player color
            Color color = gm.player_colors[cmd.player_id];

            // New Command
            if (cmd.cmd_id > last_drawn_cmd_id)
            {
                last_drawn_cmd_id = Mathf.Max(cmd.cmd_id, last_drawn_cmd_id);
                StartCoroutine(FlashMarker(marker));
                if (cmd.scored)
                {
                    // Score effect
                    Text score_marker = Instantiate(score_marker_prefab);
                    RectTransform rt = score_marker.GetComponent<RectTransform>();
                    score_marker.transform.SetParent(line, false);
                    score_marker.color = color;
                    SetMarkerPosition(rt, cmd.score_time);
                    StartCoroutine(FlashScoreMarker(score_marker));
                }
            }

            // Set marker color 
            if (cmd.valid)
            {
                marker.GetComponent<Image>().color = Color.Lerp(color, Color.black, 0);
            }
            else
            {
                marker.GetComponent<Image>().color = Color.Lerp(color, Color.black, 0.5f);
            }
        }
    }
    private void UpdateClock()
    {
        clock.text = Tools.FormatTimeAsMinSec(Time);
    }
    private IEnumerator FlashMarker(RectTransform marker)
    {
        // Shrink
        for (float t = 0; t < 1; t += UnityEngine.Time.deltaTime * 2f)
        {
            if (marker == null) break;
            float w = Mathf.Lerp(50, 1, 1-Mathf.Pow(1-t, 2));
            marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            
            yield return null;
        }

        if (marker == null) yield break;

        // Flash
        Image img = marker.GetComponent<Image>();
        Color color = img.color;
        for (int i = 0; i < 16; ++i)
        {
            if (marker == null) break;
            img.color = i % 2 == 0 ? Color.white : color;
            yield return new WaitForSeconds(0.25f);
        }
    }
    private IEnumerator FlashScoreMarker(Text marker)
    {
        // Flash
        Color color = marker.color;
        for (int i = 0; i < 16; ++i)
        {
            if (marker == null) break;
            marker.color = i % 2 == 0 ? Color.white : color;
            yield return new WaitForSeconds(0.25f);
        }

        Destroy(marker.gameObject);
    }

    // Debug
    private void LogStates()
    {
        foreach (WorldState state in key_states)
        {
            string s = "STATE " + state.time + "\n";
            for (int i = 0; i < state.planet_pops.Length; ++i)
            {
                s += "Planet " + i + "(" + state.planet_ownerIDs[i] + "): " + state.planet_pops[i] + "\n";
            }
            for (int i = 0; i < state.flights.Count; ++i)
            {
                s += "Flight " + "\n";
            }
            s += "\n";
            Tools.Log(s);
        }
    }


    // PRIVATE HELPERS

    private void SetMarkerPosition(RectTransform marker, float time)
    {
        float x = Mathf.Lerp(0, line.rect.width, time / time_length);
        marker.anchoredPosition = SetX(marker.anchoredPosition, x);
    }
    private float TimeFromMousePos(float mousex)
    {
        mousex -= line.offsetMin.x;
        mousex = Mathf.Clamp(mousex, 0, line.rect.width);

        return (mousex / line.rect.width) * time_length;
    }
    private Vector3 SetX(Vector3 v, float x)
    {
        v.x = x;
        return v;
    }
    private Vector3 SetY(Vector3 v, float y)
    {
        v.y = y;
        return v;
    }
}

public abstract class TLEvent
{
    public Flight flight;
    public PlayerCmd cmd;
}
public class TLEFlightEnd : TLEvent
{
    public TLEFlightEnd(Flight flight, PlayerCmd cmd)
    {
        this.flight = flight;
        this.cmd = cmd;
    }
}
public class TLECmd : TLEvent
{
    public TLECmd(PlayerCmd cmd)
    {
        this.cmd = cmd;
    }
}
public class TLEFlightStart : TLEvent
{
    public TLEFlightStart(Flight flight, PlayerCmd cmd)
    {
        this.flight = flight;
        this.cmd = cmd;
    }
}

