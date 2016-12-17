using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class Timeline : MonoBehaviour
{
    public int LineID { get; private set; }

    // References
    private GameManager gm;
    private CamController cam;
    private static List<Timeline> tls = new List<Timeline>();

    // UI
    private bool focused = true;
    public RectTransform cmds_parent, trs_parent;
    public RectTransform line, knob, marker_prefab, time_route_marker;
    private bool pointer_down;
    public Text clock, win_text, score_text, score_marker_prefab;
    public Image change_overlay;
    private Coroutine flash_change_routine;
    private List<Turnover> turnovers = new List<Turnover>();

    // Interaction
    private float scrape_speed = 30;
    private float scrape_speed_fast = 60;

    // Time
    public float Time { get; private set; }
    private float time_length = 100;

    // History
    private WorldState state_0;
    private LinkedList<WorldState> key_states;
    private LinkedList<PlayerCmd> player_cmds;
    private int latest_cmd_id = -1;
    private int last_drawn_cmd_id = -1;
    private float latest_cmd_time;
    private float min_cmd_dist = 5;

    private SortedList<float, TLEvent> fwd_key_events;
    private SortedList<float, TLEvent> next_fwd_key_events;
    private bool settled = false;
    private float latest_change_time = 0; // earliest time affected by latest change


    // Events
    public System.Action<Timeline> on_time_set;
    public System.Action<Timeline, float> on_history_change;


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
    public bool[] GetPlanetsReady(WorldState state)
    {
        bool[] ready = new bool[gm.planets.Length];
        for (int i = 0; i < ready.Length; ++i)
        {
            ready[i] = state.planet_ownerIDs[i] != -1;
        }

        foreach (PlayerCmd cmd in GetPlayerCmds())
        {
            if (Mathf.Abs(cmd.time - state.time) < min_cmd_dist)
            {
                if (state.planet_ownerIDs[cmd.selected_planet_id] == cmd.player_id)
                    ready[cmd.selected_planet_id] = false;
            }
        }

        return ready;
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
        tls.Add(this);

        // UI
        if (LineID == 0) CreateRouteMarkers();

        // Create initial history
        state_0 = new WorldState(0, gm.planets);
        fwd_key_events = new SortedList<float, TLEvent>(new DuplicateKeyComparer<float>());
        next_fwd_key_events = new SortedList<float, TLEvent>(new DuplicateKeyComparer<float>());

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
        Time = Mathf.Clamp(time, 0, time_length);

        // Load state
        LoadState(GetState(Time));

        // UI
        SetMarkerPosition(knob, Time);
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

        latest_change_time = latest_cmd_time;
        UpdateAllTLs(LineID);

        if (gm.CurrentTimeline == this) LoadState(GetState(Time));
    }


    // PRIVATE MODIFIERS

    private void Awake()
    {
        cam = Camera.main.GetComponent<CamController>();

        // Events
        gm = FindObjectOfType<GameManager>();
        gm.on_win += OnWin;
        gm.on_time_set += OnTimeSet; // any timeline time set

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

        // Timeline scrape key input
        if (focused)
        {
            float scrape = Input.GetAxis("Horizontal");
            if (Mathf.Abs(scrape) > 0.5f)
            {
                float speed = Input.GetKey(KeyCode.LeftShift) ? scrape_speed_fast : scrape_speed;
                SetTime(Time + scrape * UnityEngine.Time.deltaTime * speed);
                SetMarkerPosition(knob, Time);
            }
        }
        
    }

    // General World State
    private void LoadState(WorldState state)
    {
        // Update planets
        bool[] ready = GetPlanetsReady(state);
        for (int i = 0; i < gm.planets.Length; ++i)
        {
            gm.planets[i].SetPop(state.planet_pops[i], state.planet_ownerIDs[i]);
            gm.planets[i].SetReady(ready[i]);
        }

        // Update routes
        foreach (Route[] routes in gm.planet_routes)
        {
            foreach (Route route in routes)
            {
                if (route != null) route.UpdateVisuals(Time);
            }
        }

        // Destroy existing gm.fleets
        foreach (Fleet fleet in gm.fleets)
        {
            Destroy(fleet.gameObject);
        }
        gm.fleets.Clear();

        // Add new fleets
        foreach (Flight flight in state.flights)
        {
            float p = flight.GetProgress(state.time);

            Fleet fleet = Instantiate(gm.fleet_prefab);
            fleet.Initialize(flight.owner_id, flight.ships, gm.player_colors[flight.owner_id]);
            fleet.SetPosition(gm.planets[flight.start_planet_id], gm.planets[flight.end_planet_id], p);

            if (flight.flight_type == FlightType.TimeTravelSend)
                fleet.SetAlpha(Mathf.Max(0, 1 - p * 2f));
            else if (flight.flight_type == FlightType.TimeTravelRecv)
                fleet.SetAlpha(Mathf.Min(1, p*2f));

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
    private void ApplyFlightEnd(WorldState state, Flight flight, out bool scored, out bool took_planet)
    {
        scored = false;
        took_planet = false;
        state.flights.Remove(flight);

        if (flight.flight_type == FlightType.TimeTravelSend) return;
        if (flight.ships == 0) return; // ghost flight

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
                took_planet = true;
                state.planet_pops[flight.end_planet_id] = new_pop;
                state.planet_ownerIDs[flight.end_planet_id] = flight.owner_id;
            }
        }
    }

    private static void UpdateAllTLs(int origin_line_id)
    {
        bool log = tls[0].gm.debug_log_tl_remake;

        // Prep
        for (int i = 0; i < tls.Count; ++i)
        {
            tls[i].settled = false;
            if (i != origin_line_id) tls[i].latest_change_time = -1;
        }

        int iteration = 1;
        while (true)
        {
            // Update each timeline
            if (log) Tools.Log("Remake itr " + iteration);
            for (int i = 0; i < tls.Count; ++i)
            {
                tls[i].RemakeKeyStates();
            }

            // Post update - check settled, prep next pass
            for (int i = 0; i < tls.Count; ++i)
            {
                tls[i].CheckSettled(); // updates latest change time and settled
                //Tools.Log("TL " + i + " next: " + tls[i].next_fwd_key_events.Count + " settled: " + tls[i].settled);
                tls[i].fwd_key_events = tls[i].next_fwd_key_events;
                tls[i].next_fwd_key_events = new SortedList<float, TLEvent>(
                    new DuplicateKeyComparer<float>());
            }

            // Termination (are all timelines settled)
            bool all_settled = true;
            for (int i = 0; i < tls.Count; ++i)
            {
                all_settled = all_settled && tls[i].settled;
            }
            if (all_settled) break;

            ++iteration;
        }

        // Timelines up to date
        for (int i = 0; i < tls.Count; ++i)
        {
            // Event
            if (tls[i].latest_change_time >= 0)
                tls[i].OnHistoryChange(tls[i].latest_change_time);
        }

        if (log) Tools.Log("Settled");
    }
    private void RemakeKeyStates()
    {
        // Delete old key states
        key_states.Clear();

        // Add back time 0 state
        SaveKeyState(state_0);

        // Create other key states
        SortedList<float, TLEvent> key_events = new SortedList<float, TLEvent>(new DuplicateKeyComparer<float>());
        foreach (KeyValuePair<float, TLEvent> kv in fwd_key_events) key_events.Add(kv.Key, kv.Value);
        foreach (PlayerCmd cmd in player_cmds) key_events.Add(cmd.time, new TLECmd(cmd));


        while (key_events.Count > 0)
        {
            TLEvent e = key_events.Values[0];
            float time = key_events.Keys[0];
            key_events.RemoveAt(0);

            if (e as TLECmd != null)
            {
                // Player command
                WorldState state = GetState(e.cmd.time);
                Flight new_flight = e.cmd.TryToApply(state, this, gm.planets, gm.planet_routes);

               
                // Command is valid in current history
                e.cmd.valid = new_flight.ships == 0;
                key_events.Add(new_flight.end_time, new TLEFlightEnd(new_flight, e.cmd));

                if (new_flight.flight_type == FlightType.TimeTravelSend) // flight to past or future
                {
                    Flight recv_flight = Flight.MakeRecvFlight(new_flight, gm.planet_routes);
                    tls[recv_flight.tl_id].next_fwd_key_events.Add(
                        recv_flight.start_time, new TLEFlightStart(recv_flight, e.cmd));
                }
                SaveKeyState(state);


                //if (new_flight != null)
                //{
                //    // Command is valid in current history
                //    e.cmd.valid = true;
                //    key_events.Add(new_flight.end_time, new TLEFlightEnd(new_flight, e.cmd));

                //    if (new_flight.flight_type == FlightType.TimeTravelSend) // flight to past or future
                //    {
                //        Flight recv_flight = Flight.MakeRecvFlight(new_flight, gm.planet_routes);
                //        tls[recv_flight.tl_id].next_fwd_key_events.Add(
                //            recv_flight.start_time, new TLEFlightStart(recv_flight, e.cmd));
                //    }
                //    SaveKeyState(state);
                //}
                //else
                //{
                //    // Command is invalid in current history
                //    e.cmd.valid = false;
                //}
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
                    bool took_planet = false;
                    ApplyFlightEnd(state, e.flight, out scored, out took_planet);

                    if (took_planet)
                    {
                        gm.MarkConquest(e.cmd.player_id, LineID, e.flight.end_planet_id);
                    }
                    if (scored && !e.cmd.scored)
                    {
                        gm.GivePoint(e.cmd.player_id);
                        e.cmd.scored = true;
                        e.cmd.score_time = e.flight.end_time;
                    }
                    SaveKeyState(state);
                }
            }
        }
    }
    private void CheckSettled()
    {
        bool log = gm.debug_log_tl_remake;

        settled = true;
        for (int i = 0; i < Mathf.Max(next_fwd_key_events.Count, fwd_key_events.Count); ++i)
        {
            if (i >= next_fwd_key_events.Count)
            {
                latest_change_time = Mathf.Min(latest_change_time, fwd_key_events.Keys[i]);
                if (log) Tools.Log("Discrepency at " + latest_change_time + " (removed events)");
                settled = false; break;
            }
            if (i >= fwd_key_events.Count)
            {
                latest_change_time = Mathf.Min(latest_change_time, next_fwd_key_events.Keys[i]);
                if (log) Tools.Log("Discrepency at " + latest_change_time + " (new events)");
                settled = false; break;
            }
            if (next_fwd_key_events.Keys[i] != fwd_key_events.Keys[i])
            {
                latest_change_time = Mathf.Min(latest_change_time, Mathf.Min(fwd_key_events.Keys[i], next_fwd_key_events.Keys[i]));
                if (log) Tools.Log("Discrepency at " + latest_change_time + " (event time)");
                settled = false; break;
            }
            if (next_fwd_key_events.Values[i].flight.ships != fwd_key_events.Values[i].flight.ships)
            {
                //Tools.Log(prev_fwd_key_events.Values[i].flight.ships + " != " + fwd_key_events.Values[i].flight.ships);
                latest_change_time = Mathf.Min(latest_change_time, fwd_key_events.Keys[i]);
                if (log) Tools.Log("Discrepency at " + latest_change_time + string.Format(" (ship numbers: {0} vs {1})",
                    fwd_key_events.Values[i].flight.ships, next_fwd_key_events.Values[i].flight.ships));
                settled = false; break;
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
    private void OnHistoryChange(float earliest)
    {
        UpdateHistoryMarkers2();

        if (flash_change_routine != null) StopCoroutine(flash_change_routine); 
        flash_change_routine = StartCoroutine(FlashChangeOverlay(earliest));

        if (on_history_change != null) on_history_change(this, earliest);
    }
    private void OnTimeSet(Timeline line) // any timeline
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
    private void CreateRouteMarkers()
    {
        for (int i = 0; i < gm.planets.Length; ++i)
        {
            for (int j = i; j < gm.planets.Length; ++j)
            {
                Route r = gm.planet_routes[i][j];
                if (r == null) continue;
                if (r.IsTimeRoute())
                {
                    float size = Mathf.Lerp(0.5f, 2f, (r.GetTRSecond() - r.GetTRFirst()) / time_length);

                    // Right pointing arrow
                    RectTransform marker = Instantiate(time_route_marker);
                    marker.SetParent(trs_parent, false);
                    Text txt = marker.GetComponent<Text>();
                    txt.fontSize = (int)(txt.fontSize * size);
                    txt.text = ">";
                    SetMarkerPosition(marker, r.GetTRFirst());

                    // Left pointing arrow
                    marker = Instantiate(time_route_marker);
                    marker.SetParent(trs_parent, false);
                    txt = marker.GetComponent<Text>();
                    txt.text = "<";
                    txt.fontSize = (int)(txt.fontSize * size);
                    SetMarkerPosition(marker, r.GetTRSecond());
                }
            }
        }
    }
    private void UpdateHistoryMarkers2()
    {
        List<RectTransform> old_markers = new List<RectTransform>();
        foreach (Turnover old_to in turnovers)
        {
            if (old_to.flash_routine != null)
                StopCoroutine(old_to.flash_routine);
            old_markers.Add(old_to.marker);
        }
        StartCoroutine(FadeOutMarkers(old_markers));

        List<Turnover> updated_turnovers = GetTurnovers();

        foreach (Turnover to in updated_turnovers)
        {
            bool new_turnover = true;
            foreach (Turnover old_to in turnovers)
            {
                if (to.time == old_to.time && to.planet_id == old_to.planet_id)
                {
                    new_turnover = false;
                    break;
                }
            }

            RectTransform marker = Instantiate(marker_prefab);
            marker.SetParent(cmds_parent, false);
            to.marker = marker;

            SetMarkerPosition(marker, to.time);
            Vector3 p = marker.localPosition;
            p.y = ((float)to.planet_id / gm.planets.Length) * 30 - 15;
            marker.localPosition = p;

            float size = Mathf.Lerp(5, 30, to.new_pop / 200f);
            marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

            Color color = gm.player_colors[to.new_owner_id];
            marker.GetComponent<Image>().color = color;

            if (new_turnover)
                to.flash_routine = StartCoroutine(FlashMarker2(marker, size));
        }

        turnovers = updated_turnovers;
    }
    private void UpdateHistoryMarkers()
    {
        // Commands
        float prev_marker_time = -1;
        int cmd_i = 0;
        Tools.DestroyChildren(cmds_parent);

 
        foreach (PlayerCmd cmd in GetPlayerCmds())
        {
            // Determine marker time to avoid overlapping markers
            float marker_time = cmd.time;
            if (marker_time - prev_marker_time < 0.1f)
            {
                marker_time = prev_marker_time + 0.1f;
            }
            prev_marker_time = marker_time;

            // Create / reuse and position marker
            //RectTransform marker;
            //if (cmd_i < cmds_parent.childCount)
            //{
            //    marker = cmds_parent.GetChild(cmd_i).GetComponent<RectTransform>();
            //}
            //else
            //{
            //    marker = Instantiate(marker_prefab);
            //    marker.SetParent(cmds_parent, false);
            //}
            RectTransform marker, marker2;
            //marker = Instantiate(marker_prefab);
            //marker.SetParent(cmds_parent, false);
            marker2 = Instantiate(marker_prefab);
            marker2.SetParent(cmds_parent, false);

            //SetMarkerPosition(marker, marker_time);
            //float dur = Mathf.Min(5, cmd.time - 0) + Mathf.Min(5, time_length - cmd.time);
            //SetMarkerPosition(marker, Mathf.Min(Mathf.Max(cmd.time, dur/2f), time_length-dur/2f));
            //marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, DurationToTLDist(dur));

            SetMarkerPosition(marker2, cmd.time);
            marker2.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 4);
            marker2.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 4);

            // Player color
            Color color = gm.player_colors[cmd.player_id];


            //marker.GetComponent<Image>().color = Color.Lerp(color, new Color(0.3f, 0.3f, 0.3f), 0.6f);
            //marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1);
            //Vector3 p = marker.localPosition;
            //p.y = ((float)cmd.selected_planet_id / gm.planets.Length) * 55 - 25;
            //marker.localPosition = p;

            marker2.GetComponent<Image>().color = Color.Lerp(Color.clear, color, (cmd.cmd_id + 1) / (float)player_cmds.Count);
            Vector3 p = marker2.localPosition;
            p.y = ((float)cmd.selected_planet_id / gm.planets.Length) * 55 - 25;
            marker2.localPosition = p;



            // New Command
            if (cmd.cmd_id > last_drawn_cmd_id)
            {
                last_drawn_cmd_id = Mathf.Max(cmd.cmd_id, last_drawn_cmd_id);
                //StartCoroutine(FlashMarker(marker));
            }

            // Set marker color 
            if (cmd.valid)
            {
                //marker.GetComponent<Image>().color = color;
                //marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1);
                //Vector3 p = marker.localPosition;
                //p.y = ((float)cmd.selected_planet_id / gm.planets.Length) * 55 - 25;                
                //marker.localPosition = p;
            }
            else
            {
                //marker.GetComponent<Image>().color = color;
                //marker.GetComponent<Image>().color = Color.clear;
                marker2.GetComponent<Image>().color = Color.grey;
                //marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1);
            }

            ++cmd_i;
        }
    }
    private void UpdateClock()
    {
        clock.text = ((int)Time).ToString("D3");
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
        marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1);

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
    private IEnumerator FlashMarker2(RectTransform marker, float size)
    {
        // Shrink
        for (float t = 0; t < 1; t += UnityEngine.Time.deltaTime * 2f)
        {
            if (marker == null) yield break;

            float s = Mathf.Lerp(size * 4f, size, 1 - Mathf.Pow(1 - t, 2));
            marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, s);
            marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, s);

            yield return null;
        }

        if (marker == null) yield break;
        marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

        // Flash
        Image img = marker.GetComponent<Image>();
        Color color = img.color;
        for (int i = 0; i < 16; ++i)
        {
            if (marker == null) yield break;

            float s = i % 2 == 0 ? size * 2f : size;
            marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, s);
            marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, s);
            //img.color = i % 2 == 0 ? Color.white : color;
            yield return new WaitForSeconds(0.25f);
        }
    }
    private IEnumerator FadeOutMarkers(List<RectTransform> markers)
    {
        Image[] marker_imgs = new Image[markers.Count];
        Color[] start_colors = new Color[markers.Count];
        int i = 0;
        foreach (RectTransform marker in markers)
        {
            marker_imgs[i] = marker.GetComponent<Image>();
            start_colors[i] = marker_imgs[i].color;
            ++i;
        }

        // Flash
        for (i = 0; i < 8; ++i)
        {
            for (int j = 0; j < marker_imgs.Length; ++j)
            {
                marker_imgs[j].color = i % 2 == 0 ? Color.clear : start_colors[j];
            }
            yield return new WaitForSeconds(0.1f);
        }
        
        // Fade
        for (float t = 0; t < 1; t += UnityEngine.Time.deltaTime / 5f)
        {
            for (i = 0; i < marker_imgs.Length; ++i)
            {
                marker_imgs[i].color = Color.Lerp(start_colors[i], Color.clear, t);
            }
            yield return null;
        }

        // Destroy
        for (i = 0; i < marker_imgs.Length; ++i)
        {
            Destroy(marker_imgs[i].gameObject);
        }
    }
    private IEnumerator FlashChangeOverlay(float earliest)
    {
        change_overlay.gameObject.SetActive(true);
        change_overlay.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            line.rect.width * ((time_length - earliest) / time_length));
        SetMarkerPosition(change_overlay.rectTransform, earliest);

        Color start_color = Color.white;
        start_color.a = 0.3f;

        for (float t = 0; t < 1; t += UnityEngine.Time.deltaTime)
        {
            //float f = Mathf.Lerp(0, 1, 1-Mathf.Pow(1-t, 2));
            change_overlay.color = Color.Lerp(start_color, Color.clear, t);
            yield return null;
        }

        change_overlay.gameObject.SetActive(false);
    }

    private List<Turnover> GetTurnovers()
    {
        List<Turnover> turnovers = new List<Turnover>();

        WorldState last_state = state_0;
        foreach (WorldState state in key_states)
        {
            for (int i = 0; i < state.planet_pops.Length; ++i)
            {
                if (state.planet_ownerIDs[i] != last_state.planet_ownerIDs[i])
                {
                    if (state.planet_ownerIDs[i]
                        != last_state.planet_ownerIDs[i])
                    {
                        Turnover to = new Turnover();
                        to.time = state.time;
                        to.planet_id = i;
                        to.old_owner_id = last_state.planet_ownerIDs[i];
                        to.new_owner_id = state.planet_ownerIDs[i];
                        to.new_pop = state.planet_pops[i];

                        turnovers.Add(to);
                    }
                }
            }
            last_state = state;
        }

        return turnovers;
    }


    // PRIVATE HELPERS

    private void SetMarkerPosition(RectTransform marker, float time)
    {
        float x = Mathf.Lerp(0, line.rect.width, time / time_length);
        marker.anchoredPosition = SetX(marker.anchoredPosition, x);
    }
    private float DurationToTLDist(float duration)
    {
        return (duration / time_length) * line.rect.width;
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

public class Turnover
{
    public float time;
    public int planet_id;
    public int new_pop;
    public int old_owner_id;
    public int new_owner_id;
    public RectTransform marker;
    public Coroutine flash_routine;
}
