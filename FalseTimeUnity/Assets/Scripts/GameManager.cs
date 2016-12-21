﻿using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // Debug
    public bool debug_solo = false;
    public bool debug_powers = false;
    public bool debug_log_tl_remake = false;

    // General
    private bool initialized = false;
    private bool game_over = false;
    private int flags_to_win;

    // Timelines
    public Timeline CurrentTimeline { get; private set; }

    // Players
    public Color[] player_colors;
    public string[] player_names;
    public int num_humans = 2;
    public int num_bots = 2;

    private int players_registered = 0;
    public Dictionary<int, Player> players; // keys are player ids
    private int[] player_scores; // flag counts
    private bool[][][] player_flags; // player_id, timeline_id, planet_id

    // References
    public SeedManager seed_manager;
    public Timeline[] timelines;
    public Transform connection_screen;

    // Planets
    public int num_planets = 50;
    [System.NonSerialized] public Planet[] planets; // indexed by planet id
    public Planet planet_prefab;
    public Route route_prefab;
    [System.NonSerialized] public float[][] planet_dists;
    [System.NonSerialized] public Route[][] planet_routes;

    // Fleets
    [System.NonSerialized] public List<Fleet> fleets;
    public Fleet fleet_prefab, ghost_fleet_prefab;

    // Events
    public System.Action on_initialized;
    public System.Action<Player> on_player_registered;
    public System.Action on_all_players_registered;
    public System.Action<Timeline> on_time_set;
    public System.Action<Timeline, float> on_history_change;
    public System.Action<int, float> on_win;
    public System.Action<NewFlagEvent> on_new_flag;


    // PUBLIC ACCESSORS

    public bool IsInitialized()
    {
        return initialized;
    }
    public bool ArePlayersRegistered()
    {
        return players_registered == GetNumPlayers();
    }
    public bool IsGameOver()
    {
        return game_over;
    }
    public bool IsGamePlaying()
    {
        return ArePlayersRegistered() && !IsGameOver();
    }
    
    public Planet[] GetPlanets()
    {
        return planets;
    }
    public Timeline[] GetTimelines()
    {
        return timelines;
    }

    public int GetNumPlayers()
    {
        return num_bots + num_humans;
    }
    public Player GetLocalHumanPlayer()
    {
        foreach (Player p in players.Values)
        {
            if (p.isLocalPlayer && !p.ai_controlled) return p;
        }
        return null;
    }
    public Dictionary<int, Player> GetPlayers()
    {
        return players;
    }

    public int GetFlagsToWin()
    {
        return flags_to_win;
    }
    public int GetPlayerScore(Player player)
    {   
        return player_scores[player.player_id];
    }
    public int GetPlayerScore(int player_id)
    {
        return player_scores[player_id];
    }
    public int GetWinner()
    {
        // Win by having high enough score
        foreach (int player_id in players.Keys)
        {
            if (player_scores[player_id] >= flags_to_win)
                return player_id;
        }

        return -1;
    }


    // PUBLIC MODIFIERS

    public void RegisterPlayer(Player player)
    {
        players[player.player_id] = player;
        ++players_registered;

        if (on_player_registered != null) on_player_registered(player);
        if (ArePlayersRegistered())
        {
            // All players registered
            if (on_all_players_registered != null)
                on_all_players_registered();
            connection_screen.gameObject.SetActive(false);
        }

        Tools.Log("Registered player " + player.player_id 
            + (player.ai_controlled ? " (AI)" : ""), Color.blue);
    }
    public void OnWin(int winner, int win_line, float win_time)
    {
        game_over = true;

        CurrentTimeline = timelines[win_line];
        CurrentTimeline.SetTime(win_time);

        if (on_win != null) on_win(winner, win_time);
    }
    public void SwitchTimeline()
    {
        int i = CurrentTimeline.LineID;
        i = (i + 1) % timelines.Length;
        timelines[i].SetTime(timelines[i].Time);
    }


    // PRIVATE MODIFIERS / HELPERS

    private void Awake()
    {
        // Debug options
        if (!Debug.isDebugBuild)
        {
            debug_solo = false;
            debug_powers = false;
        }

        // Players
        if (GetNumPlayers() < 2) Debug.LogError("num players must be > 1");
        players = new Dictionary<int, Player>();
        player_scores = new int[GetNumPlayers()];

        // Fleets
        fleets = new List<Fleet>();

        // World generation
        StartCoroutine(GenerateWorld());
    }

    // Initialization
    private void OnWorldGenerated()
    {
        // Flag counts / score
        player_flags = new bool[GetNumPlayers()][][];
        for (int i = 0; i < GetNumPlayers(); ++i)
        {
            player_flags[i] = new bool[timelines.Length][];
            for (int j = 0; j < timelines.Length; ++j)
            {
                player_flags[i][j] = new bool[planets.Length];
                for (int k = 0; k < planets.Length; ++k)
                {
                    bool owns = timelines[j].GetState(0).planet_ownerIDs[k] == i;
                    if (owns) MarkFlag(i, j, k, 0);
                }
            }
        }

        // Scores
        flags_to_win = (int)(num_planets * 1.8f);
    }
    private IEnumerator GenerateWorld()
    {
        while (!seed_manager.seed_set) yield return null;

        // Generate Planets
        Random.InitState(seed_manager.seed);
        GeneratePlanets();
        CreateRoutes();

        // Timelines
        for (int i = 0; i < timelines.Length; ++i)
        {
            timelines[i].Initialize(i);
            timelines[i].on_time_set += OnTimeSet;
            timelines[i].on_history_change += OnHistoryChange;
        }
        CurrentTimeline = timelines[0];
        CurrentTimeline.SetTime(0);

        // Done
        OnWorldGenerated();
        initialized = true;
        if (on_initialized != null)
        {
            on_initialized();
        }
    }
    private void GeneratePlanets()
    {
        int n = num_planets; // Random.Range(15, 15);
        planets = new Planet[n];

        
        // Positions
        List<Vector2> positions = GeneratePlanetPositions(n);
        Vector2 center = new Vector2();
        foreach (Vector2 pos in positions)
        {
            center += pos;
        }
        center /= n;
        for (int i = 0; i < n; ++i)
        {
            // shift position to center planets on this object
            positions[i] += (Vector2)transform.position - center;
        }

        // Create neutral planets
        for (int i = 0; i < n; ++i)
        {
            Planet planet = Instantiate(planet_prefab);
            planet.transform.SetParent(transform);

            float size = Random.Range(0.75f, 2.25f);
            int pop = (int)(size * Random.value * 10);
            planet.Initialize(i, size, pop, -1);
            planet.transform.position = positions[i] * 3.5f;

            planets[i] = planet;
        }

        // Select player start planets
        for (int i = 0; i < GetNumPlayers(); ++i)
        {
            int planet_id = Random.Range(0, n);
            while (planets[planet_id].OwnerID != -1)
                planet_id = (planet_id + 1) % planets.Length;

            planets[planet_id].SetSize(1.5f);
            planets[planet_id].SetPop(10, i);
        }

        // Store planet distances
        planet_dists = new float[planets.Length][];
        for (int i = 0; i < planets.Length; ++i)
        {
            planet_dists[i] = new float[planets.Length];
            for (int j = 0; j < planets.Length; ++j)
            {
                if (i == j) continue;
                planet_dists[i][j] = Vector2.Distance(planets[i].transform.position, planets[j].transform.position)
                    - planets[i].Radius - planets[j].Radius;
            }
        }
    }
    private void CreateRoutes()
    {
        planet_routes = new Route[planets.Length][];
        for (int i = 0; i < planets.Length; ++i)
            planet_routes[i] = new Route[planets.Length];

        for (int i = 0; i < planets.Length; ++i)
        {
            for (int j = i+1; j < planets.Length; ++j)
            {
                float dist = planet_dists[i][j];
                if (dist < 3.5f)
                {
                    Route route = Instantiate(route_prefab);
                    route.transform.SetParent(transform);
                    route.Initialize(planets[i], planets[j]);

                    planet_routes[i][j] = route;
                    planet_routes[j][i] = route;
                }
            }
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

    // Flags
    private void MarkFlag(int player_id, int timeline_id, int planet_id, float time)
    {
        if (!player_flags[player_id][timeline_id][planet_id])
        {
            // New flag
            player_flags[player_id][timeline_id][planet_id] = true;
            player_scores[player_id] += 1;

            // UI
            if (CurrentTimeline.LineID == timeline_id)
                planets[planet_id].ShowFlag(player_id);

            // Event
            NewFlagEvent e = new NewFlagEvent();
            e.timeline_id = timeline_id;
            e.time = time;
            e.player_id = player_id;
            e.planet_id = planet_id;
            if (on_new_flag != null) on_new_flag(e);
        }   
    }
    private void UpdateFlagsUI()
    {
        for (int i = 0; i < GetNumPlayers(); ++i)
        {
            for (int j = 0; j < planets.Length; ++j)
            {
                planets[j].ShowFlag(i, player_flags[i][CurrentTimeline.LineID][j]);
            }
        }
    }

    // Events
    private void OnTimeSet(Timeline line)
    {
        if (CurrentTimeline != line)
        {
            CurrentTimeline = line;
            UpdateFlagsUI();
        }
        if (on_time_set != null) on_time_set(line);
    }
    private void OnHistoryChange(Timeline line, float earliest)
    {
        if (on_history_change != null) on_history_change(line, earliest);

        // Mark new flags
        foreach (Timeline tl in timelines)
        {
            foreach (Turnover to in tl.GetTurnovers())
            {
                MarkFlag(to.new_owner_id, tl.LineID, to.planet_id, to.time);
            }
        }
    }

}

public class NewFlagEvent
{
    public int timeline_id;
    public float time;
    public int planet_id;
    public int player_id;
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

    public Flight TryToApply(WorldState state, Timeline line, Planet[] planets, Route[][] routes)
    {
        Route route = routes[selected_planet_id][target_planet_id];
        bool transfer = state.planet_ownerIDs[selected_planet_id] == state.planet_ownerIDs[target_planet_id];
        int ships = Mathf.CeilToInt(state.planet_pops[selected_planet_id] / 2f);
        valid = false;

        // Can't send ships from enemy planet
        if (state.planet_ownerIDs[selected_planet_id] == player_id)
        {
            // Can't send less than 1 ship
            if (ships >= 1)
            {
                // Can only send ships without route if between friendly planets
                if (route != null || transfer)
                {
                    valid = true;
                }
            }
        }

        // Make Flight
        Flight flight;

        if (valid)
        {
            flight = new Flight(state.planet_ownerIDs[selected_planet_id],
                ships, planets[selected_planet_id], planets[target_planet_id], time, line.LineID);

            // Modify state
            state.planet_pops[selected_planet_id] -= ships;
        }
        else
        {
            // Invalid command - send ghost flight to indicate invalid command (0 ships)
            flight = new Flight(player_id, 0, planets[selected_planet_id],
                planets[target_planet_id], time, line.LineID);
        }

        // Time travelling
        bool time_traveling = route != null && route.IsTimeRoute(time);
        if (time_traveling) flight.flight_type = FlightType.TimeTravelSend;

        // Add to state
        state.flights.Add(flight);

        return flight;
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
    public int tl_id;

    public Flight(int owner_id, int ships, Planet start_planet, Planet end_planet, float start_time, int tl_id)
    {
        this.owner_id = owner_id;
        this.ships = ships;
        this.start_planet_id = start_planet.PlanetID;
        this.end_planet_id = end_planet.PlanetID;

        float dist = Vector2.Distance(start_planet.transform.position, end_planet.transform.position)
            - start_planet.Radius - end_planet.Radius;

        this.start_time = start_time;
        end_time = start_time + dist / speed;
        this.tl_id = tl_id;
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
        tl_id = to_copy.tl_id;
    }
    public static Flight MakeRecvFlight(Flight send_flight, Route[][] routes)
    {
        Flight f = new Flight(send_flight);
        f.flight_type = FlightType.TimeTravelRecv;
        f.start_time = routes[f.start_planet_id][f.end_planet_id].GetTimeTravelTime(send_flight.start_time);
        f.end_time = f.start_time + (send_flight.end_time - send_flight.start_time);

        if (routes[f.start_planet_id][f.end_planet_id].IsCrossing()) f.tl_id = 1 - f.tl_id;

        return f;
    }

    public float GetProgress(float time)
    {
        return (time - start_time) / (end_time - start_time);
    }
}
public enum FlightType { Normal, TimeTravelSend, TimeTravelRecv }