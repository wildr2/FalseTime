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
    public Text clock, win_text, score_text;

    // Interaction
    private bool paused = true;

    // Time
    public float Time { get; private set; }
    private float time_length = 120;

    // History
    private WorldState state_0;
    private LinkedList<WorldState> key_states;
    private LinkedList<PlayerCmd> player_cmds;

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
                    % gm.num_players;
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
        SaveCommand(cmd);
        RemakeKeyStates();
        LoadState(GetState(Time));
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

        // Have server check win condition
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
        // Delete old key states
        key_states.Clear();

        // Add back time 0 state
        SaveKeyState(state_0);

        // Create other key states
        SortedList<float, Pair<PlayerCmd, Flight>> flight_ends = new SortedList<float, Pair<PlayerCmd, Flight>>(new DuplicateKeyComparer<float>());
        LinkedListNode<PlayerCmd> next_cmd = player_cmds.First;

        while (true)
        {
            Flight f = flight_ends.Count > 0 ? flight_ends.Values[0].Second : null;
            PlayerCmd cmd = next_cmd == null ? null : next_cmd.Value;
            if (f == null && cmd == null) break;

            if (f == null || (cmd != null && cmd.time < f.end_time))
            {
                // Player command (flight start)
                WorldState state = GetState(cmd.time);
                Flight new_flight = cmd.TryToApply(state, gm.planets);
                if (new_flight != null)
                {
                    // Command is valid in current history
                    cmd.valid = true;
                    flight_ends.Add(new_flight.end_time, new Pair<PlayerCmd, Flight>(cmd, new_flight));
                    SaveKeyState(state);
                }
                else
                {
                    cmd.valid = false;
                }
                next_cmd = next_cmd.Next;
            }
            else if (f.end_time <= GetEndTime())
            {
                // Flight end
                WorldState state = GetState(f.end_time);
                bool scored = false;
                ApplyFlightEnd(state, f, out scored);
                SaveKeyState(state);

                PlayerCmd flight_end_cmd = flight_ends.Values[0].First;
                if (scored && !flight_end_cmd.scored)
                {
                    gm.GivePoint(flight_end_cmd.player_id);
                    flight_end_cmd.scored = true;
                }

                flight_ends.RemoveAt(0);
            }
        }

        // Event
        OnHistoryChange();
        
        // Debug
        if (log_states) LogStates();
    }
    private void RemakeKeyStates2()
    {
        // Create flight events
        SortedList<float, Flight> flight_events = new SortedList<float, Flight>();

        foreach (PlayerCmd cmd in player_cmds)
        {
            WorldState state = GetState(cmd.time);
            int n = state.planet_pops[cmd.selected_planet_id] / 2;

            Flight flight = new Flight(
                gm.planets[cmd.selected_planet_id].OwnerID,
                n,
                gm.planets[cmd.selected_planet_id],
                gm.planets[cmd.target_planet_id], cmd.time);

            flight_events.Add(flight.start_time, flight);
            flight_events.Add(flight.end_time, flight);
        }

        // Save key states for flight events
        foreach (KeyValuePair<float, Flight> fe in flight_events)
        {
            float time = fe.Key;
            Flight f = fe.Value;

            if (time == f.start_time)
            {
                // Flight start
                WorldState state = GetState(time);
                state.planet_pops[f.start_planet_id] -= f.ships;
                SaveKeyState(state);
            }
            else
            {
                // Flight end
                WorldState state = GetState(time);
                state.flights.Remove(f);

                if (f.owner_id == state.planet_ownerIDs[f.end_planet_id])
                {
                    // Transfer
                    state.planet_pops[f.end_planet_id] += f.ships;
                }
                else
                {
                    // Attack
                    int new_pop = state.planet_pops[f.end_planet_id] - f.ships;
                    if (new_pop >= 0) state.planet_pops[f.end_planet_id] = new_pop;
                    else
                    {
                        // Allegiance change
                        state.planet_pops[f.end_planet_id] = -new_pop;
                        state.planet_ownerIDs[f.end_planet_id] = f.owner_id;
                    }
                }

                SaveKeyState(state);
            }
        }
    } // NOT USED
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
                if (state.planet_ownerIDs[flight.end_planet_id] != -1) scored = true;
                state.planet_pops[flight.end_planet_id] = -new_pop;
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
        foreach (PlayerCmd cmd in GetPlayerCmds())
        {
            RectTransform marker = Instantiate(marker_prefab);
            marker.SetParent(cmds_parent, false);
            SetMarkerPosition(marker, cmd.time);
            //marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, line.rect.height * );

            Color color = gm.player_colors[cmd.player_id];

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
}
