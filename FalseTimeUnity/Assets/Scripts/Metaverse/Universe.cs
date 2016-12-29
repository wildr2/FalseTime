using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class Universe
{
    public int UniverseID { get; private set; }

    // References
    private static Metaverse mv;

    // Const
    public const float TimeLength = 100;

    // History
    private UVState state_0;
    private LinkedList<UVState> key_states;

    // Player command history
    private LinkedList<PlayerCmd> player_cmds;
    private int latest_cmd_id = -1;
    private float latest_cmd_time;

    private SortedList<float, TLEvent> fwd_key_events;
    private SortedList<float, TLEvent> next_fwd_key_events;
    private bool settled = false;
    private float latest_change_time = 0; // earliest time affected by latest change

    // Turnovers
    private List<Turnover> turnovers = new List<Turnover>();

    // Events
    public System.Action<Universe, PlayerCmd> on_new_cmd;
    public System.Action<Universe, float> on_history_change; // universe, earliest change


    // PUBLIC ACCESSORS

    public UVState GetState(float time)
    {
        UVState recent = GetMostRecentKeyState(time).Value;
        UVState newstate = new UVState(recent);
        newstate.time = time;

        // Interpolate
        float time_since = time - recent.time;

        for (int i = 0; i < mv.Planets.Length; ++i)
        {
            int growth = Mathf.FloorToInt(mv.Planets[i].GetPopPerSecond(
                newstate.planet_ownerIDs[i]) * time_since);

            newstate.planet_pops[i] += growth;
        }

        return newstate;
    }
    public LinkedList<UVState> GetKeyStates()
    {
        return key_states;
    }
    public LinkedList<PlayerCmd> GetPlayerCmds()
    {
        return player_cmds;
    }
    public float GetLatestCmdTime()
    {
        return latest_cmd_time;
    }
    public List<Turnover> GetTurnovers()
    {
        return turnovers;
    }


    // PUBLIC MODIFIERS

    public Universe(int id, Metaverse mv)
    {
        UniverseID = id;
        Universe.mv = mv;

        // Player commands
        player_cmds = new LinkedList<PlayerCmd>();

        // Key states
        key_states = new LinkedList<UVState>();

        // Create initial history
        state_0 = new UVState(0, mv.Planets);
        fwd_key_events = new SortedList<float, TLEvent>(new DuplicateKeyComparer<float>());
        next_fwd_key_events = new SortedList<float, TLEvent>(new DuplicateKeyComparer<float>());

        // Rotate starting planets
        for (int i = 0; i < state_0.planet_ownerIDs.Length; ++i)
        {
            if (state_0.planet_ownerIDs[i] != -1)
            {
                state_0.planet_ownerIDs[i] = (state_0.planet_ownerIDs[i] + id)
                    % DataManager.Instance.GetNumPlayers();
            }
        }

        // Create initial key states
        RemakeKeyStates();
    }
    public void AddPlayerCmd(PlayerCmd cmd)
    {
        cmd.cmd_id = ++latest_cmd_id;
        latest_cmd_time = cmd.time;
        SaveCommand(cmd);

        latest_change_time = latest_cmd_time;
        UpdateAllUVs(UniverseID);

        if (on_new_cmd != null) on_new_cmd(this, cmd);
    }


    // PRIVATE MODIFIERS

    private static void UpdateAllUVs(int origin_uv_id)
    {
        bool log = DataManager.Instance.debug_log_tl_remake;

        Universe[] uvs = mv.Universes;

        // Prep
        for (int i = 0; i < uvs.Length; ++i)
        {
            uvs[i].settled = false;
            if (i != origin_uv_id) uvs[i].latest_change_time = TimeLength + 1;
        }

        int iteration = 1;
        while (true)
        {
            // Update each universe's history
            if (log) Tools.Log("Remake itr " + iteration);
            for (int i = 0; i < uvs.Length; ++i)
            {
                uvs[i].RemakeKeyStates();
            }

            // Post update - check settled, prep next pass
            for (int i = 0; i < uvs.Length; ++i)
            {
                uvs[i].CheckSettled(); // updates latest change time and settled
                //Tools.Log("TL " + i + " next: " + uvs[i].next_fwd_key_events.Count + " settled: " + uvs[i].settled);
                uvs[i].fwd_key_events = uvs[i].next_fwd_key_events;
                uvs[i].next_fwd_key_events = new SortedList<float, TLEvent>(
                    new DuplicateKeyComparer<float>());
            }

            // Termination (are all histories settled)
            bool all_settled = true;
            for (int i = 0; i < uvs.Length; ++i)
            {
                all_settled = all_settled && uvs[i].settled;
            }
            if (all_settled) break;

            ++iteration;
        }

        // Histories up to date
        for (int i = 0; i < uvs.Length; ++i)
        {
            // Update history information
            uvs[i].turnovers = uvs[i].FindTurnovers();

            // Event
            if (uvs[i].latest_change_time <= TimeLength)
            {
                if (uvs[i].on_history_change != null)
                    uvs[i].on_history_change(uvs[i], uvs[i].latest_change_time);
            }
        }

        if (log) Tools.Log("Settled");
    }
    private List<Turnover> FindTurnovers()
    {
        List<Turnover> turnovers = new List<Turnover>();

        UVState last_state = state_0;
        foreach (UVState state in key_states)
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

    // Writing keystates
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
                UVState state = GetState(e.cmd.time);
                Flight new_flight = ApplyPlayerCmd(e.cmd, state);

                key_events.Add(new_flight.end_time, new TLEFlightEnd(new_flight, e.cmd));

                if (new_flight.flight_type == FlightType.TimeTravelSend) // flight to past or future
                {
                    Flight recv_flight = Flight.MakeRecvFlight(new_flight, mv.Routes);
                    mv.Universes[recv_flight.uv_id].next_fwd_key_events.Add(
                        recv_flight.start_time, new TLEFlightStart(recv_flight, e.cmd));
                }
                SaveKeyState(state);
            }
            else if (e as TLEFlightStart != null)
            {
                // Flight start (from command at other time)
                UVState state = GetState(e.flight.start_time);
                state.flights.Add(e.flight);
                key_events.Add(e.flight.end_time, new TLEFlightEnd(e.flight, e.cmd));
                SaveKeyState(state);
            }
            else if (e as TLEFlightEnd != null)
            {
                // Flight end
                if (e.flight.end_time <= TimeLength)
                {
                    UVState state = GetState(e.flight.end_time);

                    bool scored = false;
                    bool took_planet = false;
                    ApplyFlightEnd(state, e.flight, out scored, out took_planet);

                    //if (took_planet)
                    //{
                    //    gm.MarkConquest(e.cmd.PlayerID, UniverseID, e.flight.end_planet_id);
                    //}
                    if (scored && !e.cmd.scored)
                    {
                        e.cmd.scored = true;
                        e.cmd.score_time = e.flight.end_time;
                    }
                    SaveKeyState(state);
                }
            }
        }
    }
    private void SaveKeyState(UVState state)
    {
        LinkedListNode<UVState> recent = GetMostRecentKeyState(state.time);

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
    private LinkedListNode<UVState> GetMostRecentKeyState(float time)
    {
        if (key_states.Count == 0) return null;

        // Find most recent (to time) state
        LinkedListNode<UVState> node = key_states.Last;
        while (node != null && node.Value.time > time)
        {
            node = node.Previous;
        }
        return node;
    }
    private void ApplyFlightEnd(UVState state, Flight flight, out bool scored, out bool took_planet)
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
    private void CheckSettled()
    {
        bool log = DataManager.Instance.debug_log_tl_remake;

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
    private Flight ApplyPlayerCmd(PlayerCmd cmd, UVState state)
    {
        if (cmd.time != state.time) Debug.LogError("cmd and state time must be equal");

        Route route = mv.Routes[cmd.selected_planet_id][cmd.target_planet_id];
        bool transfer = state.planet_ownerIDs[cmd.selected_planet_id]
            == state.planet_ownerIDs[cmd.target_planet_id];
        int ships = Mathf.CeilToInt(state.planet_pops[cmd.selected_planet_id] / 2f);
        cmd.valid = false;

        // Can't send ships from enemy planet
        if (state.planet_ownerIDs[cmd.selected_planet_id] == cmd.player_id)
        {
            // Can't send less than 1 ship
            if (ships >= 1)
            {
                // Can only send ships without route if between friendly planets
                if (route != null || transfer)
                {
                    cmd.valid = true;
                }
            }
        }

        // Make Flight
        Flight flight;

        if (cmd.valid)
        {
            flight = new Flight(
                state.planet_ownerIDs[cmd.selected_planet_id],
                ships, 
                mv.Planets[cmd.selected_planet_id], 
                mv.Planets[cmd.target_planet_id],
                cmd.time, UniverseID);

            // Modify state
            state.planet_pops[cmd.selected_planet_id] -= ships;
        }
        else
        {
            // Invalid command - send ghost flight to indicate invalid command (0 ships)
            flight = new Flight(cmd.player_id,
                0,
                mv.Planets[cmd.selected_planet_id],
                mv.Planets[cmd.target_planet_id],
                cmd.time, UniverseID);
        }

        // Time travelling
        bool time_traveling = route != null && route.Wormhole != null
            && route.Wormhole.IsOpen(cmd.time);

        if (time_traveling) flight.flight_type = FlightType.TimeTravelSend;

        // Add to state
        state.flights.Add(flight);

        return flight;
    }

}

public class UVState
{
    public float time;
    public int[] planet_pops;
    public int[] planet_ownerIDs;
    public List<Flight> flights;

    public UVState(float time, Planet[] Planets)
    {
        this.time = time;

        flights = new List<Flight>();

        planet_pops = new int[Planets.Length];
        planet_ownerIDs = new int[Planets.Length];

        for (int i = 0; i < Planets.Length; ++i)
        {
            planet_pops[i] = Planets[i].Pop;
            planet_ownerIDs[i] = Planets[i].OwnerID;
        }
    }
    public UVState(UVState to_copy)
    {
        time = to_copy.time;

        int planets_n = to_copy.planet_pops.Length;
        planet_pops = new int[planets_n];
        planet_ownerIDs = new int[planets_n];
        System.Array.Copy(to_copy.planet_pops, planet_pops, planets_n);
        System.Array.Copy(to_copy.planet_ownerIDs, planet_ownerIDs, planets_n);

        flights = new List<Flight>();
        foreach (Flight flight in to_copy.flights)
        {
            //flights.Add(new Flight(flight));
            flights.Add(flight);
        }
    }
}
public class PlayerCmd
{
    public int cmd_id;
    public bool scored = false;
    public float score_time;
    public bool valid;
    public float time;
    public int player_id;
    public int selected_planet_id;
    public int target_planet_id;

    public PlayerCmd(float time, int selected_planet_id, int target_planet_id, int player_id)
    {
        this.time = time;
        this.selected_planet_id = selected_planet_id;
        this.target_planet_id = target_planet_id;
        this.player_id = player_id;
    }
    public PlayerCmd(float time, Planet selected_planet, Planet target_planet, int player_id)
    {
        this.time = time;
        this.selected_planet_id = selected_planet.PlanetID;
        this.target_planet_id = target_planet.PlanetID;
        this.player_id = player_id;
    }
}
public class Flight
{
    public const float speed = 0.55f; // units per second

    public int owner_id;
    public int ships;
    public int start_planet_id;
    public int end_planet_id;
    public float start_time, end_time;
    public FlightType flight_type = FlightType.Normal;
    public int uv_id;

    public Flight(int owner_id, int ships, Planet start_planet, Planet end_planet, float start_time, int uv_id)
    {
        this.owner_id = owner_id;
        this.ships = ships;
        this.start_planet_id = start_planet.PlanetID;
        this.end_planet_id = end_planet.PlanetID;

        float dist = Vector2.Distance(start_planet.transform.position, end_planet.transform.position)
            - start_planet.Radius - end_planet.Radius;

        this.start_time = start_time;
        end_time = start_time + dist / speed;
        this.uv_id = uv_id;
    }
    public Flight(Flight to_copy)
    {
        owner_id = to_copy.owner_id;
        ships = to_copy.ships;
        start_planet_id = to_copy.start_planet_id;
        end_planet_id = to_copy.end_planet_id;
        start_time = to_copy.start_time;
        end_time = to_copy.end_time;
        flight_type = to_copy.flight_type;
        uv_id = to_copy.uv_id;
    }
    public static Flight MakeRecvFlight(Flight send_flight, Route[][] routes)
    {
        Flight f = new Flight(send_flight);
        f.flight_type = FlightType.TimeTravelRecv;

        Route r = routes[f.start_planet_id][f.end_planet_id];
        if (r.Wormhole != null)
        {
            UnivTime entry = new UnivTime(f.uv_id, f.start_time);
            UnivTime exit = r.Wormhole.GetExit(entry);
            f.start_time = exit.time;
            f.end_time = f.start_time + (send_flight.end_time - send_flight.start_time);
            f.uv_id = exit.universe;
        }

        return f;
    }

    public float GetProgress(float time)
    {
        return (time - start_time) / (end_time - start_time);
    }
}
public enum FlightType { Normal, TimeTravelSend, TimeTravelRecv }

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

    /// <summary>
    /// Whether this turnover is the same as to2 other than the population number and the previous owner
    /// </summary>
    /// <param name="to2"></param>
    /// <returns></returns>
    public bool IsSameEventAs(Turnover to2)
    {
        return new_owner_id == to2.new_owner_id &&
               planet_id == to2.planet_id &&
               time == to2.time;
    }
}
