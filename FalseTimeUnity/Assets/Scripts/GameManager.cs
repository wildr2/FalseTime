using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // Debug
    public bool debug_solo = false;
    public bool debug_powers = false;

    // General
    private bool initialized = false;
    private bool game_over = false;
    private int points_to_win = 10;

    // Timelines
    public Timeline CurrentTimeline { get; private set; }

    // Players
    public Color[] player_colors;
    public string[] player_names;
    public int num_players = 2;
    public int num_bots = 2;

    private int players_registered = 0;
    public Dictionary<int, Player> players; // keys are player ids
    private Dictionary<int, int> player_scores;

    // References
    public SeedManager seed_manager;
    public Timeline[] timelines;
    public Transform connection_screen;

    // Planets
    [System.NonSerialized] public Planet[] planets; // indexed by planet id
    public Planet planet_prefab;
    public LineRenderer route_prefab;
    [System.NonSerialized] public float[][] planet_dists;
    [System.NonSerialized] public bool[][] planet_routes;

    // Fleets
    [System.NonSerialized] public List<Fleet> fleets;
    public Fleet fleet_prefab;

    // Events
    public System.Action on_initialized;
    public System.Action<Player> on_player_registered;
    public System.Action<Timeline> on_time_set;
    public System.Action<Timeline> on_history_change;
    public System.Action<int, float> on_win;


    // PUBLIC ACCESSORS

    public bool IsInitialized()
    {
        return initialized;
    }
    public bool ArePlayersRegistered()
    {
        return players_registered == num_players;
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

    public Player GetLocalPlayer()
    {
        foreach (Player p in players.Values)
        {
            if (p.isLocalPlayer) return p;
        }
        return null;
    }
    public Dictionary<int, Player> GetPlayers()
    {
        return players;
    }
    public int GetPlayerScore(Player player)
    {
        return player_scores[player.player_id];
    }
    public int GetWinner(int line, float time)
    {
        // Win by having high enough score
        foreach (int player_id in players.Keys)
        {
            if (player_scores[player_id] >= points_to_win)
                return player_id;
        }

        // Win by clearing enemy at some line / time
        //return timelines[line].GetStateWinner(time);

        return -1;
    }


    // PUBLIC MODIFIERS

    public void RegisterPlayer(Player player)
    {
        players[player.player_id] = player;
        player_scores[player.player_id] = 0;
        ++players_registered;

        if (on_player_registered != null) on_player_registered(player);
        if (ArePlayersRegistered()) connection_screen.gameObject.SetActive(false);

        Tools.Log("Registered player " + player.player_id 
            + (player.ai_controlled ? " (AI)" : ""), Color.blue);
    }
    public void GivePoint(int player_id)
    {
        player_scores[player_id] += 1;
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
        CurrentTimeline = timelines[i];
        CurrentTimeline.SetTime(CurrentTimeline.Time);
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
        if (num_players < 2) Debug.LogError("num_players must be > 1");
        players = new Dictionary<int, Player>();
        player_scores = new Dictionary<int, int>();

        // Fleets
        fleets = new List<Fleet>();

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
            planet.transform.position = positions[i] * 2.5f;

            planets[i] = planet;
        }

        // Select player start planets
        for (int i = 0; i < Mathf.Max(num_players, 2); ++i)
        {
            int planet_id = Random.Range(0, n);
            while (planets[planet_id].OwnerID != -1)
                planet_id = (planet_id + 1) % planets.Length;

            planets[planet_id].Initialize(planet_id, 1, 10, i);
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
        planet_routes = new bool[planets.Length][];
        for (int i = 0; i < planets.Length; ++i)
            planet_routes[i] = new bool[planets.Length];

        for (int i = 0; i < planets.Length; ++i)
        {
            for (int j = i+1; j < planets.Length; ++j)
            {
                float dist = planet_dists[i][j];
                if (dist < 3f)
                {
                    planet_routes[i][j] = true;
                    planet_routes[j][i] = true;

                    //Debug.DrawLine(planets[i].transform.position, planets[j].transform.position, new Color(1, 1, 1, 0.25f), 1000);
                    LineRenderer route = Instantiate(route_prefab);
                    route.transform.SetParent(transform);
                    route.SetPosition(0, planets[i].transform.position);
                    route.SetPosition(1, planets[j].transform.position);
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

    // Events
    private void OnTimeSet(Timeline line)
    {
        CurrentTimeline = line;
        if (on_time_set != null) on_time_set(line);
    }
    private void OnHistoryChange(Timeline line)
    {
        if (on_history_change != null) on_history_change(line);
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

    public Flight TryToApply(WorldState state, Planet[] planets)
    {
        // Can't send ships from enemy planet
        if (state.planet_ownerIDs[selected_planet_id] != player_id) return null;

        int ships = Mathf.CeilToInt(state.planet_pops[selected_planet_id] / 2f);

        // Can't send less than 1 ship
        if (ships < 1) return null;

        // Create flight
        Flight flight = new Flight(state.planet_ownerIDs[selected_planet_id],
            ships, planets[selected_planet_id], planets[target_planet_id], time);

        // Modify state
        state.planet_pops[selected_planet_id] -= ships;
        state.flights.Add(flight);

        return flight;
    }
}
public class Flight
{
    public const float speed = 0.25f; // units per second

    public int owner_id;
    public int ships;
    public int start_planet_id;
    public int end_planet_id;
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