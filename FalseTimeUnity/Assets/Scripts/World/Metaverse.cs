using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 
/// Not created until Start()
/// </summary>
public class Metaverse : MonoBehaviour
{
    private DataManager dm;
    public SeedManager seed_manager;

    private bool is_created = false;

    // Current View
    public View View { get; private set; }

    // Universes
    public Universe[] Universes { get; private set; }

    // Planets
    public Planet planet_prefab;
    public Planet[] Planets { get; private set; } // [planet_id]
    private float[][] planet_dists; // [planet_id][planet_id]
    private float min_cmd_dist = 5;

    // Routes
    public Route route_prefab;
    public Route[][] Routes { get; private set; } // [planet_id][planet_id]

    // Fleets
    public Fleet fleet_prefab, ghost_fleet_prefab;
    private List<Fleet> fleets;

    // Flags
    private bool[][][] player_flags; // player_id, universe_id, planet_id

    // Events
    public System.Action on_created;
    public System.Action<NewFlagEvent> on_new_flag;
    public System.Action<Universe, float> on_history_change; // universe, earliest change
    public System.Action<View> on_view_set;


    // PUBLIC ACCESSORS

    public bool IsCreated()
    {
        return is_created;
    }
    
    public bool[] GetPlanetsReady(Universe uv, UVState state)
    {
        bool[] ready = new bool[Planets.Length];
        for (int i = 0; i < ready.Length; ++i)
        {
            ready[i] = state.planet_ownerIDs[i] != -1;
        }

        foreach (PlayerCmd cmd in uv.GetPlayerCmds())
        {
            if (Mathf.Abs(cmd.time - state.time) < min_cmd_dist)
            {
                if (state.planet_ownerIDs[cmd.selected_planet_id] == cmd.player_id)
                    ready[cmd.selected_planet_id] = false;
            }
        }

        return ready;
    }
    public float GetPlanetDistance(Planet p1, Planet p2)
    {
        return planet_dists[p1.PlanetID][p2.PlanetID];
    }
    public float GetPlanetDistance(int p1_id, int p2_id)
    {
        return planet_dists[p1_id][p2_id];
    }


    // PUBLIC MODIFIERS

    public void SetView(float time)
    {
        SetView(time, View.Universe);
    }
    public void SetView(float time, int universe_id)
    {
        SetView(time, Universes[universe_id]);
    }
    public void SetView(float time, Universe universe)
    {
        if (!IsCreated()) Debug.LogError("The metaverse hasn't yet been created");

        time = Mathf.Clamp(time, 0, Universe.TimeLength);

        View = new View(universe, universe.GetState(time));
        LoadView();
        if (on_view_set != null) on_view_set(View);
    }
    

    // PRIVATE MODIFIERS

    private void Awake()
    {
        dm = DataManager.Instance;
    }
    private void Start()
    {
        if (seed_manager.seed_set) Create();
        else seed_manager.on_seed_set += Create;
    }

    private void Create()
    {
        // Planets and routes are the same across all universes
        CreatePlanets();
        CreateRoutes();

        // Universes must be initialized after planets and routes
        // (to create state history)
        CreateUniverses();

        // Other
        InitFlags();
        fleets = new List<Fleet>();

        // Done
        is_created = true;
        SetView(0, 0);

        if (on_created != null)
            on_created();
    }
    private void CreateUniverses()
    {
        Universes = new Universe[dm.num_universes];
        for (int i = 0; i < dm.num_universes; ++i)
        {
            Universes[i] = new Universe(i, this);
            Universes[i].on_history_change += OnHistoryChange;
        }
    }
    private void CreatePlanets()
    {
        int n = dm.num_planets; // Random.Range(15, 15);
        Planets = new Planet[n];


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
            // shift position to center Planets on this object
            positions[i] += (Vector2)transform.position - center;
        }

        // Create neutral Planets
        for (int i = 0; i < n; ++i)
        {
            Planet planet = Instantiate(planet_prefab);
            planet.transform.SetParent(transform);

            float size = Random.Range(0.75f, 2.25f);
            int pop = (int)(size * Random.value * 10);
            planet.Initialize(i, size, pop, -1);
            planet.transform.position = positions[i] * 3.5f;

            Planets[i] = planet;
        }

        // Select player start Planets
        for (int i = 0; i < dm.GetNumPlayers(); ++i)
        {
            int planet_id = Random.Range(0, n);
            while (Planets[planet_id].OwnerID != -1)
                planet_id = (planet_id + 1) % Planets.Length;

            Planets[planet_id].SetSize(1.5f);
            Planets[planet_id].SetPop(10, i);
        }

        // Store planet distances
        planet_dists = new float[Planets.Length][];
        for (int i = 0; i < Planets.Length; ++i)
        {
            planet_dists[i] = new float[Planets.Length];
            for (int j = 0; j < Planets.Length; ++j)
            {
                if (i == j) continue;
                planet_dists[i][j] = Vector2.Distance(Planets[i].transform.position, Planets[j].transform.position)
                    - Planets[i].Radius - Planets[j].Radius;
            }
        }
    }
    private void CreateRoutes()
    {
        Routes = new Route[Planets.Length][];
        for (int i = 0; i < Planets.Length; ++i)
            Routes[i] = new Route[Planets.Length];

        for (int i = 0; i < Planets.Length; ++i)
        {
            for (int j = i + 1; j < Planets.Length; ++j)
            {
                float dist = planet_dists[i][j];
                if (dist < 3.5f)
                {
                    Route route = Instantiate(route_prefab);
                    route.transform.SetParent(transform);
                    route.Initialize(Planets[i], Planets[j]);

                    Routes[i][j] = route;
                    Routes[j][i] = route;
                }
            }
        }
    }
    private List<Vector2> GeneratePlanetPositions(int n)
    {
        // Nodes
        int try_count = 0;

        List<IVector2> nodes = new List<IVector2>();
        nodes.Add(new IVector2(0, 0));

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
    private void InitFlags()
    {
        // Flag counts / score
        player_flags = new bool[dm.GetNumPlayers()][][];
        for (int i = 0; i < dm.GetNumPlayers(); ++i)
        {
            player_flags[i] = new bool[Universes.Length][];
            for (int j = 0; j < Universes.Length; ++j)
            {
                player_flags[i][j] = new bool[Planets.Length];
                for (int k = 0; k < Planets.Length; ++k)
                {
                    bool owns = Universes[j].GetState(0).planet_ownerIDs[k] == i;
                    if (owns) MarkFlag(i, j, k, 0);
                }
            }
        }
    }

    private void LoadView()
    {
        UpdatePlanets();
        UpdateRoutes();
        UpdateFleets();
        UpdateFlags();
    }
    private void UpdatePlanets()
    {
        UVState state = View.State;

        bool[] ready = GetPlanetsReady(View.Universe, state);
        for (int i = 0; i < Planets.Length; ++i)
        {
            Planets[i].SetPop(state.planet_pops[i], state.planet_ownerIDs[i]);
            Planets[i].SetReady(ready[i]);
        }
    }
    private void UpdateRoutes()
    {
        foreach (Route[] routes in Routes)
        {
            foreach (Route route in routes)
            {
                if (route != null) route.UpdateVisuals(View.Time);
            }
        }
    }
    private void UpdateFleets()
    {
        UVState state = View.State;

        // Destroy existing fleets
        foreach (Fleet fleet in fleets)
        {
            Destroy(fleet.gameObject);
        }
        fleets.Clear();

        // Add new fleets
        foreach (Flight flight in state.flights)
        {
            float p = flight.GetProgress(state.time);

            Fleet fleet;
            if (flight.ships == 0)
                fleet = Instantiate(ghost_fleet_prefab);
            else
                fleet = Instantiate(fleet_prefab);

            fleet.Initialize(flight.owner_id, flight.ships, dm.GetPlayerColor(flight.owner_id));
            fleet.SetPosition(Planets[flight.start_planet_id], Planets[flight.end_planet_id], p);

            if (flight.flight_type == FlightType.TimeTravelSend)
                fleet.SetAlpha(Mathf.Max(0, 1 - p * 2f));
            else if (flight.flight_type == FlightType.TimeTravelRecv)
                fleet.SetAlpha(Mathf.Min(1, p * 2f));

            fleets.Add(fleet);
        }
    }
    private void UpdateFlags()
    {
        for (int i = 0; i < dm.GetNumPlayers(); ++i)
        {
            for (int j = 0; j < Planets.Length; ++j)
            {
                Planets[j].ShowFlag(i, player_flags[i][View.Universe.UniverseID][j]);
            }
        }
    }

    private void MarkFlag(int player_id, int universe_id, int planet_id, float time)
    {
        if (!player_flags[player_id][universe_id][planet_id])
        {
            // New flag
            player_flags[player_id][universe_id][planet_id] = true;

            if (View != null && View.Universe.UniverseID == universe_id)
            {
                Planets[planet_id].ShowFlag(player_id);
            }

            // Event
            NewFlagEvent e = new NewFlagEvent();
            e.universe_id = universe_id;
            e.time = time;
            e.player_id = player_id;
            e.planet_id = planet_id;
            if (on_new_flag != null) on_new_flag(e);
        }
    }
    private void MarkNewFlags(Universe uv)
    {
        foreach (Turnover to in uv.GetTurnovers())
        {
            MarkFlag(to.new_owner_id, uv.UniverseID, to.planet_id, to.time);
        }
    }

    private void OnHistoryChange(Universe uv, float earliest)
    {
        MarkNewFlags(uv);

        // Update view
        if (View.Universe == uv) SetView(View.Time);

        // Collect history change events from all universes and send a new event
        if (on_history_change != null)
            on_history_change(uv, earliest);
    }

}

public class View
{
    public Universe Universe { get; private set; }
    public UVState State { get; private set; }

    public float Time
    {
        get
        {
            return State.time;
        }
    }

    public View(Universe universe, UVState state)
    {
        Universe = universe;
        State = state;
    }
}

public class NewFlagEvent
{
    public int universe_id;
    public float time;
    public int planet_id;
    public int player_id;
}
