using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;


public class GameManager : MonoBehaviour
{
    // Dev
    public bool log_states = false;

    // General
    private bool initialized = false;
    private bool game_over = false;

    // Players
    public Color[] player_colors;
    public string[] player_names;
    private int num_players = 2;
    private Player[] players;

    // References
    public SeedManager seed_manager;
    private Timeline timeline;
    
    // History
    private WorldState state_0;
    private LinkedList<WorldState> key_states;
    private LinkedList<PlayerCmd> player_cmds;

    // Planets
    private Planet[] planets;
    public Planet planet_prefab;

    // Fleets
    private List<Fleet> fleets;
    public Fleet fleet_prefab;

    // Events
    public System.Action on_initialized;
    public System.Action on_history_change;
    public System.Action<int, float> on_win;


    // PUBLIC ACCESSORS

    public bool IsInitialized()
    {
        return initialized;
    }
    public bool IsGameOver()
    {
        return game_over;
    }
    public Timeline GetTimeline()
    {
        return timeline;
    }
    public Planet[] GetPlanets()
    {
        return planets;
    }
    public LinkedList<WorldState> GetKeyStates()
    {
        return key_states;
    }
    public LinkedList<PlayerCmd> GetPlayerCmds()
    {
        return player_cmds;
    }
    public List<PlayerCmd> GetInvalidPlayerCmds()
    {
        List<PlayerCmd> list = new List<PlayerCmd>();
        foreach (PlayerCmd cmd in player_cmds)
        {
            if (!cmd.IsValid(GetState(cmd.time))) list.Add(cmd);
        }
        return list;
    }

    public int GetStateWinner(float time)
    {
        WorldState state = GetState(time);
        HashSet<int> alive_players = new HashSet<int>();

        for (int i = 0; i < planets.Length; ++i)
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


    // PUBLIC MODIFIERS

    public void RegisterPlayer(Player player)
    {
        players[player.player_id] = player;
        Tools.Log("Registered player " + player.player_id, Color.blue);
    }
    public void AddPlayerCmd(PlayerCmd cmd)
    {
        SaveCommand(cmd);
        RemakeKeyStates();
        LoadState(GetState(timeline.Time));
    }
    public void OnWin(int winner, float win_time)
    {
        game_over = true;
        if (on_win != null) on_win(winner, win_time);
    }


    // PRIVATE MODIFIERS / HELPERS

    private void Awake()
    {
        // Players
        players = new Player[num_players];

        // Fleets
        fleets = new List<Fleet>();

        // Player commands
        player_cmds = new LinkedList<PlayerCmd>();

        // Timeline
        timeline = FindObjectOfType<Timeline>();
        timeline.on_time_set += OnTimeSet;

        // Key states
        key_states = new LinkedList<WorldState>();

        // World generation
        StartCoroutine(GenerateWorld());
    }

    // Initialization
    private IEnumerator GenerateWorld()
    {
        while (!seed_manager.seed_set) yield return null;

        // Generate Planets
        Random.seed = seed_manager.seed;
        GeneratePlanets();

        // Create initial history
        state_0 = new WorldState(0, planets);
        RemakeKeyStates();

        // Done
        initialized = true;
        if (on_initialized != null)
        {
            on_initialized();
        }
    }
    private void GeneratePlanets()
    {
        int n = Random.Range(15, 20);
        planets = new Planet[n];

        List<Vector2> positions = GeneratePlanetPositions(n);

        // Create neutral planets
        for (int i = 0; i < n; ++i)
        {
            Planet planet = Instantiate(planet_prefab);
            planet.transform.SetParent(transform);

            float size = Random.Range(0.28f, 2f);
            int pop = (int)(size * Random.value * 10);
            planet.Initialize(i, size, pop, -1);
            planet.transform.position = positions[i] * 2;

            planets[i] = planet;
        }

        // Select player start planets
        for (int i = 0; i < 2; ++i)
        {
            int planet_id = Random.Range(0, n);
            while (planets[planet_id].OwnerID != -1)
                planet_id = (planet_id + 1) % planets.Length;

            planets[planet_id].Initialize(planet_id, 1, 10, i);
        }
    }
    private List<Vector2> GeneratePlanetPositions(int n)
    {
        // Nodes
        int try_count = 0;

        List<IVector2> nodes = new List<IVector2>();
        nodes.Add(new IVector2(0,0));
        
        while (nodes.Count < n)
        {
            IVector2 origin = nodes[Random.Range(0, nodes.Count)];

            for (int i = 0; i < 10; ++i)
            {
                Vector2 candidate_f = new Vector2(origin.x, origin.y) + Tools.RandomDirection2D() * 10;
                IVector2 candidate = new IVector2(candidate_f);

                bool good = true;
                foreach (IVector2 node in nodes)
                {
                    int dist = IVector2.DistanceManhatten(node, candidate);

                    if (dist < 10)
                    {
                        good = false;
                        break;
                    }
                }
                if (good)
                {
                    nodes.Add(candidate);
                    break;
                }
            }


            // Check if failing
            ++try_count;
            if (try_count > n * 10f)
            {
                Debug.LogError("Taking too long to generate planet positions");
                break;
            }
        }


        List<Vector2> list = new List<Vector2>();
        foreach (IVector2 node in nodes)
            list.Add(new Vector2(node.x, node.y) * 0.1f);

        return list;
    }

    // General World State
    private void LoadState(WorldState state)
    {
        // Update planets
        for (int i = 0; i < planets.Length; ++i)
        {
            planets[i].SetPop(state.planet_pops[i], state.planet_ownerIDs[i]);
        }

        // Destroy existing fleets
        foreach (Fleet fleet in fleets)
        {
            Destroy(fleet.gameObject);
        }
        fleets.Clear();

        // Add new fleets
        foreach (Flight flight in state.flights)
        {
            Fleet fleet = Instantiate(fleet_prefab);
            fleet.Initialize(flight.owner_id, flight.ships, player_colors[flight.owner_id]);
            fleet.SetPosition(planets[flight.start_planet_id], planets[flight.end_planet_id], flight.GetProgress(state.time));
            fleets.Add(fleet);
        }

        // Have server check win condition
    }
    private WorldState GetState(float time)
    {
        WorldState recent = GetMostRecentKeyState(time).Value;
        WorldState newstate = new WorldState(recent);
        newstate.time = time;

        // Interpolate
        float time_since = time - recent.time;

        for (int i = 0; i < planets.Length; ++i)
        {
            int growth = Mathf.FloorToInt(planets[i].GetPopPerSecond(newstate.planet_ownerIDs[i]) * time_since);
            newstate.planet_pops[i] += growth;
        }

        //foreach (Flight flight in newstate.flights)
        //{
        //    flight.progress += flight.progress_rate * time_since; 
        //}

        return newstate;
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
        SortedList<float, Flight> flight_ends = new SortedList<float, Flight>(new DuplicateKeyComparer<float>());
        LinkedListNode<PlayerCmd> next_cmd = player_cmds.First;

        while (true)
        {
            Flight f = flight_ends.Count > 0 ? flight_ends.Values[0] : null;
            PlayerCmd cmd = next_cmd == null ? null : next_cmd.Value;
            if (f == null && cmd == null) break;

            if (f == null || (cmd != null && cmd.time < f.end_time))
            {
                // Player command (flight start)
                WorldState state = GetState(cmd.time);
                Flight new_flight = ApplyCommand(state, cmd);
                if (new_flight != null)
                {
                    // Command is valid in current history
                    flight_ends.Add(new_flight.end_time, new_flight);
                    SaveKeyState(state);
                }
                next_cmd = next_cmd.Next;
            }
            else
            {
                // Flight end
                WorldState state = GetState(f.end_time);
                ApplyFlightEnd(state, f);
                SaveKeyState(state);

                flight_ends.RemoveAt(0);
            }
        }

        if (on_history_change != null) on_history_change();

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
                planets[cmd.selected_planet_id].OwnerID,
                n,
                planets[cmd.selected_planet_id],
                planets[cmd.target_planet_id], cmd.time);

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
    private void ApplyFlightEnd(WorldState state, Flight flight)
    {
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
                state.planet_pops[flight.end_planet_id] = -new_pop;
                state.planet_ownerIDs[flight.end_planet_id] = flight.owner_id;
            }
        }
    }
    private Flight ApplyCommand(WorldState state, PlayerCmd cmd)
    {
        int select_id = cmd.selected_planet_id;
        int target_id = cmd.target_planet_id;
        int n = state.planet_pops[select_id] / 2;

        // Check command validity
        if (!cmd.IsValid(state)) return null;

        // Send ships on flight
        state.planet_pops[select_id] -= n;

        Flight flight = new Flight(state.planet_ownerIDs[select_id], n, planets[select_id], planets[target_id], cmd.time);
        state.flights.Add(flight);
        return flight;
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
    private void OnTimeSet(float time)
    {
        LoadState(GetState(time));
    }

    // Dev
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
}

public class WorldState
{
    public float time;
    public int[] planet_pops;
    public int[] planet_ownerIDs;
    public List<Flight> flights;

    public WorldState(float time, Planet[] planets)
    {
        this.time = time;

        flights = new List<Flight>();

        planet_pops = new int[planets.Length];
        planet_ownerIDs = new int[planets.Length];

        for (int i = 0; i < planets.Length; ++i)
        {
            planet_pops[i] = planets[i].Pop;
            planet_ownerIDs[i] = planets[i].OwnerID;
        }
    }
    public WorldState(WorldState to_copy)
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

    public bool IsValid(WorldState state)
    {
        // Can't send ships from enemy planet
        if (state.planet_ownerIDs[selected_planet_id] != player_id) return false;

        // Can't send less than 1 ship
        if (state.planet_pops[selected_planet_id] / 2 < 1) return false;

        return true;
    }
}
public class Flight
{
    public const float speed = 1; // units per second

    public int owner_id;
    public int ships;
    public int start_planet_id;
    public int end_planet_id;
    //public float progress;
    //public float progress_rate;
    public float start_time, end_time;

    public Flight(int owner_id, int ships, Planet start_planet, Planet end_planet, float start_time)
    {
        this.owner_id = owner_id;
        this.ships = ships;
        this.start_planet_id = start_planet.PlanetID;
        this.end_planet_id = end_planet.PlanetID;

        float dist = Vector2.Distance(start_planet.transform.position, end_planet.transform.position)
            - start_planet.Radius - end_planet.Radius;

        this.start_time = start_time;
        //progress = 0;
        //progress_rate = speed / dist;
        end_time = start_time + dist / speed;
    }
    public Flight(Flight to_copy)
    {
        owner_id = to_copy.owner_id;
        ships = to_copy.ships;
        start_planet_id = to_copy.start_planet_id;
        end_planet_id = to_copy.end_planet_id;
        //progress = to_copy.progress;
        start_time = to_copy.start_time;
        end_time = to_copy.end_time;
        //progress_rate = to_copy.progress_rate;
    }

    public float GetProgress(float time)
    {
        return (time - start_time) / (end_time - start_time);
    }
}

class FlightEndComparer : IEqualityComparer<Flight>
{
    public bool Equals(Flight f1, Flight f2)
    {
        return f1.end_time == f2.end_time;
    }

    public int GetHashCode(Flight f)
    {
        return f.end_time.GetHashCode();
    }
}