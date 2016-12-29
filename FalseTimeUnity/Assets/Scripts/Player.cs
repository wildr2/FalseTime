using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class Player : NetworkBehaviour
{
    public enum ActionType { None, Attack, Transfer, Wormhole }

    // General
    [SyncVar] private int player_id = -1; // assume arbitrary numbers?
    public int PlayerID
    {
        get
        {
            return player_id;
        }
        set
        {
            player_id = value;
        }
    }
    public bool AI { get; set; }

    // References
    private GameManager gm;
    private Metaverse mv;

    // Selection
    private Planet pointed_planet;
    private Planet selected_planet;
    private ActionType highlighted_action;

    // Power
    private float power = 0;
    private static readonly Dictionary<ActionType, float> req_power
        = new Dictionary<ActionType, float>()
    {
        { ActionType.None, 0 },
        { ActionType.Attack, 1 },
        { ActionType.Transfer, 0.1f },
        { ActionType.Wormhole, 1.5f }
    };
    private int max_power = 6; // num bars
    private float seconds_per_power = 15; // seconds per 1 bar of power 

    // Events
    public System.Action<float> on_power_change;


    // PUBLIC ACCESSORS

    public ActionType GetHighlightedAction()
    {
        return highlighted_action;
    }
    public float GetPower()
    {
        return power;
    }
    public float GetPowerMax()
    {
        return max_power;
    }
    public float GetReqPower()
    {
        return req_power[highlighted_action];
    }
    public bool HasEnoughPower()
    {
        return power >= req_power[highlighted_action];
    }


    // PRIVATE MODIFIERS

    private void Awake()
    {
        gm = FindObjectOfType<GameManager>();
        mv = FindObjectOfType<Metaverse>();

        // Events
        mv.on_new_flag += OnNewFlag;
        mv.on_view_set += OnViewSet;

        StartCoroutine(Initialize());
    }

    private IEnumerator Initialize()
    {
        while (!(mv.IsCreated() && PlayerID > -1))
            yield return null;

        if (isLocalPlayer)
        {
            if (!AI)
            {
                // Planet interaction
                foreach (Planet planet in mv.Planets)
                {
                    // Planet mouse events
                    planet.on_pointer_enter += OnPlanetMouseEnter;
                    planet.on_pointer_exit += OnPlanetMouseExit;
                }

                StartCoroutine(HumanUpdate());
            }
            else StartCoroutine(AIUpdate());
        }

        SetPower(0);

        gm.RegisterPlayer(this);
    }
    private IEnumerator HumanUpdate()
    {
        while (true)
        {
            while (gm.State != MatchState.Play) yield return null;

            // Interaction
            bool click = Input.GetMouseButtonDown(0);

            if (click)
            {
                if (pointed_planet != null)
                {
                    // Click planet
                    OnPlanetClick(pointed_planet);
                }
                else
                {
                    // Click away
                    OnClickAway();
                }
            }

            yield return null;
        }
    }
    private IEnumerator AIUpdate()
    {
        while (true)
        {
            while (gm.State != MatchState.Play || DataManager.Instance.debug_solo)
                yield return null;

            PlayerCmd best_cmd = null;
            Universe best_cmd_uv = null;
            float best_score = float.MinValue;

            // Find best command
            foreach (Universe uv in mv.Universes)
            {
                for (int time = 0; time < Universe.TimeLength; ++time)
                {
                    UVState state = uv.GetState(time);
                    bool[] planets_ready = mv.GetPlanetsReady(uv, state);

                    for (int i = 0; i < mv.Planets.Length; ++i)
                    {
                        if (state.planet_ownerIDs[i] != player_id) continue; // skip non owned planets
                        if (!planets_ready[i]) continue; // skip planets that can't be commanded now

                        for (int j = 0; j < mv.Planets.Length; ++j)
                        {
                            if (mv.Routes[i][j] == null) continue; // target planet cannot be selected planet

                            float flight_time = mv.GetPlanetDistance(i,j) / Flight.speed;
                            UVState projected_state = uv.GetState(time + flight_time + 0.1f);

                            int ships_to_send = Mathf.CeilToInt(state.planet_pops[i] / 2f);

                            if (state.planet_ownerIDs[j] == player_id) continue; // skip transfers
                            if (projected_state.planet_ownerIDs[j] == player_id) continue; // skip transfers (attacks that become transfers)
                            if (ships_to_send < projected_state.planet_pops[j] + 2) continue; // skip non takeovers
                            

                            float score = 0;

                            // Current time
                            score -= (time / Universe.TimeLength) * 1f;

                            // Taking enemy planet
                            if (projected_state.planet_ownerIDs[j] != -1) score += 30;

                            // Units remaining on target planet
                            //score += (ships_to_send + projected_state.planet_pops[j]) * 0.15f;

                            // Units remaining on selected planet
                            //score += (state.planet_pops[i] - ships_to_send) * 0.15f;

                            // Target planet size
                            score += mv.Planets[j].Size * 5f;


                            // Compare to current best command
                            if (score > best_score)
                            {
                                best_cmd = new PlayerCmd(time, i, j, player_id);
                                best_cmd_uv = uv;
                                best_score = score;
                            }
                            //Tools.Log("j: " + j);
                        }
                        //Tools.Log("i: " + i);
                    }

                    //Tools.Log("time: " + time);
                    yield return null;
                }
                //Tools.Log("uv: " + uv.UniverseID);
            }

            if (best_cmd != null)
            {
                highlighted_action = ActionType.Attack;
                while (gm.State != MatchState.Play || !HasEnoughPower()) yield return null;

                // Do best action
                CmdIssuePlayerCmd(player_id, best_cmd.selected_planet_id, best_cmd.target_planet_id,
                    best_cmd_uv.UniverseID, best_cmd.time);

                // Cost
                UseReqPower();
            }

            yield return new WaitForSeconds(Random.Range(0, 5));
        }
    }
    private void Update()
    {
        if (isLocalPlayer) LocalUpdate();
    }
    private void LocalUpdate()
    {
        if (gm.State != MatchState.Play) return;

        // Power Growth
        SetPower(Mathf.Min(power + 1f / seconds_per_power * Time.deltaTime, max_power));
    }

    private void DeselectPlanet()
    {
        if (selected_planet != null)
        {
            selected_planet.HideHighlight();
            selected_planet = null;
        }
    }
    private void SetPower(float value)
    {
        power = DataManager.Instance.debug_powers ? max_power : value;
        if (on_power_change != null) on_power_change(power);
    }
    private void UseReqPower()
    {
        SetPower(power - req_power[highlighted_action]);
    }

    // Events
    private void OnClickAway()
    {
        DeselectPlanet();
    }

    private void OnPlanetClick(Planet planet)
    {
        if (gm.State != MatchState.Play) return;

        if (selected_planet == null)
        {
            if (planet.OwnerID == player_id || DataManager.Instance.debug_solo)
            {
                if (planet.Ready)
                {
                    // Select planet
                    selected_planet = planet;
                    selected_planet.ShowHighlight(new Color(0.75f, 0.75f, 0.75f));
                }
            }
        }
        else if (planet != selected_planet)
        {
            if (selected_planet.Ready)
            {
                if (planet.OwnerID == selected_planet.OwnerID)
                {
                    // Transfer
                    // Attack along route
                    if (HasEnoughPower())
                    {
                        // Issue player command 
                        CmdIssuePlayerCmd(selected_planet.OwnerID, selected_planet.PlanetID,
                            planet.PlanetID, mv.View.Universe.UniverseID, mv.View.Time);

                        // Cost
                        UseReqPower();

                        // UI
                        DeselectPlanet();
                    }
                }
                else if (mv.Routes[selected_planet.PlanetID][planet.PlanetID] != null)
                {
                    // Attack along route
                    if (HasEnoughPower())
                    {
                        // Issue player command 
                        CmdIssuePlayerCmd(selected_planet.OwnerID, selected_planet.PlanetID,
                            planet.PlanetID, mv.View.Universe.UniverseID, mv.View.Time);

                        // Cost
                        UseReqPower();

                        // UI
                        //selected_planet.ShowReady(false);
                        DeselectPlanet();
                    }
                }
            }
        }
    }
    private void OnPlanetMouseEnter(Planet planet)
    {
        pointed_planet = planet;

        // Don't reselect selected planet
        if (planet == selected_planet) return;

        if (selected_planet == null)
        {
            // Highlight planet to select
            planet.ShowHighlight(new Color(0.5f, 0.5f, 0.5f));
        }
        else if (gm.State == MatchState.Play)
        {
            DataManager dm = DataManager.Instance;

            // Highlight planet to target
            Route route = mv.Routes[selected_planet.PlanetID][planet.PlanetID];

            if (route != null && route.Wormhole != null && route.Wormhole.IsOpen(mv.View.Time))
            {
                highlighted_action = ActionType.Wormhole;
                planet.ShowHighlight(dm.GetActionColor(highlighted_action));
            }
            else if (planet.OwnerID == selected_planet.OwnerID)
            {
                highlighted_action = ActionType.Transfer;
                planet.ShowHighlight(dm.GetActionColor(highlighted_action));
            }
            else if (mv.Routes[selected_planet.PlanetID][planet.PlanetID] != null)
            {
                highlighted_action = ActionType.Attack;
                planet.ShowHighlight(dm.GetActionColor(highlighted_action));
            }
            else
            {
                highlighted_action = ActionType.None;
                planet.ShowHighlight(new Color(0.5f, 0.5f, 0.5f));
            }
        }
    }
    private void OnPlanetMouseExit(Planet planet)
    {
        pointed_planet = null;

        if (planet != selected_planet)
        {
            // Unhighlight not selected planet
            planet.HideHighlight();
            highlighted_action = ActionType.None;
        }
            
    }

    private void OnViewSet(View view)
    {
        // check if selection is still valid
        if (selected_planet != null)
        {
            if (selected_planet.OwnerID != PlayerID)
            {
                // Invalid selection
                DeselectPlanet();
            }
        }
    }
    private void OnNewFlag(NewFlagEvent e)
    {
        if (isServer)
        {
            CmdCheckForWin();
        }
    }


    // Networking
    [Command]
    private void CmdIssuePlayerCmd(int player_id, int selected_planet_id,
        int target_planet_id, int univ_id, float time)
    {
        RpcReceivePlayerCmd(player_id, selected_planet_id, target_planet_id, univ_id, time);
    }
    [ClientRpc]
    private void RpcReceivePlayerCmd(int player_id, int selected_planet_id,
        int target_planet_id, int univ_id, float time)
    {
        PlayerCmd cmd = new PlayerCmd(time, selected_planet_id, target_planet_id, player_id);
        mv.Universes[univ_id].AddPlayerCmd(cmd);
    }

    [Command]
    private void CmdCheckForWin()
    {
        int winner = gm.GetWinner();
        if (winner >= 0)
        {
            // Win
            RpcInformWin(winner);
        }
    }
    [ClientRpc]
    private void RpcInformWin(int winner)
    {
        gm.OnWin(winner);
        DeselectPlanet();
    }
}
