using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class Player : NetworkBehaviour
{
    // General
    [SyncVar] public int player_id; // assume arbitrary numbers
    public bool ai_controlled = false;
    private bool initialized = false;

    // References
    private GameManager gm;

    // Selection
    private Planet pointed_planet;
    private Planet selected_planet;

    // Power
    private float power_bar_seconds = 30;
    private int max_power = 3;
    private float req_power = 1;
    private float power = 0;

    // Events
    public System.Action<float> on_power_change;


    // PUBLIC ACCESSORS

    public float GetPower()
    {
        return power;
    }
    public float GetPowerMax()
    {
        return max_power;
    }
    public float GetPowerReq()
    {
        return req_power;
    }


    // PRIVATE MODIFIERS

    private void Start()
    {
        gm = FindObjectOfType<GameManager>();
        StartCoroutine(Initialize());
        if (isLocalPlayer) gm.on_history_change += OnHistoryChange;
    }

    private IEnumerator Initialize()
    {
        while (!gm.IsInitialized()) yield return null;

        if (isLocalPlayer)
        {
            if (!ai_controlled)
            {
                gm.on_time_set += OnTimeSet;

                foreach (Planet planet in gm.GetPlanets())
                {
                    // Planet mouse events
                    planet.on_pointer_enter += OnPlanetMouseEnter;
                    planet.on_pointer_exit += OnPlanetMouseExit;
                }

                StartCoroutine(HumanUpdate());
            }
            else StartCoroutine(AIUpdate());
        }

        gm.RegisterPlayer(this);
        
        SetPower(0);

        initialized = true;
    }
    private IEnumerator HumanUpdate()
    {
        while (true)
        {
            while (!gm.IsGamePlaying()) yield return null;

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

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // Switch timeline
                gm.SwitchTimeline();
            }

            yield return null;
        }
    }
    private IEnumerator AIUpdate()
    {
        while (true)
        {
            while (!gm.IsGamePlaying() || gm.debug_solo) yield return null;

            PlayerCmd best_cmd = null;
            Timeline best_cmd_line = null;
            float best_score = float.MinValue;

            // Find best command
            float power_on_start_search = power;

            foreach (Timeline line in gm.GetTimelines())
            {
                for (int time = 0; time < line.GetEndTime(); ++time)
                {
                    WorldState state = line.GetState(time);
                    UpdateRequiredPower(line, time);

                    for (int i = 0; i < gm.planets.Length; ++i)
                    {
                        if (state.planet_ownerIDs[i] != player_id) continue; // skip non owned planets
                        for (int j = 0; j < gm.planets.Length; ++j)
                        {
                            if (gm.planet_routes[i][j] == null) continue; // target planet cannot be selected planet
                            if (req_power > power_on_start_search) continue; // skip too expensive commands

                            float flight_time = gm.planet_dists[i][j] / Flight.speed;
                            WorldState projected_state = line.GetState(time + flight_time + 0.1f);

                            int ships_to_send = Mathf.CeilToInt(state.planet_pops[i] / 2f);

                            if (projected_state.planet_ownerIDs[j] == player_id) continue; // skip transfers
                            if (ships_to_send < projected_state.planet_pops[j] + 2) continue; // skip non takeovers
                            

                            float score = 0;

                            // Current time
                            score -= (time / line.GetEndTime()) * 1f;

                            // Command cost
                            score -= req_power;

                            // Taking enemy planet
                            if (projected_state.planet_ownerIDs[j] != -1) score += 30;

                            // Units remaining on target planet
                            //score += (ships_to_send + projected_state.planet_pops[j]) * 0.15f;

                            // Units remaining on selected planet
                            //score += (state.planet_pops[i] - ships_to_send) * 0.15f;

                            // Target planet size
                            score += gm.planets[j].Size * 5f;


                            // Compare to current best command
                            if (score > best_score)
                            {
                                best_cmd = new PlayerCmd(time, i, j, player_id);
                                best_cmd_line = line;
                                best_score = score;
                            }
                            //Tools.Log("j: " + j);
                        }
                        //Tools.Log("i: " + i);
                    }

                    //Tools.Log("time: " + time);
                    yield return null;
                }
                //Tools.Log("line: " + line.LineID);
            }

            //Tools.Log("here");
            if (best_cmd != null)
            {
                //Tools.Log("p" + player_id + " Command");

                // Do best action
                CmdIssuePlayerCmd(player_id, best_cmd.selected_planet_id, best_cmd.target_planet_id,
                    best_cmd_line.LineID, best_cmd.time);

                // Cost
                SetPower(power - req_power);
            }

            yield return new WaitForSeconds(Random.Range(0, 5));
        }
    }
    private void Update()
    {
        if (!isLocalPlayer) return;
        if (!gm.IsGamePlaying()) return;

        // Power Growth
        if (!ai_controlled) UpdateRequiredPower(gm.CurrentTimeline, gm.CurrentTimeline.Time);
        SetPower(Mathf.Min(power + 1f / power_bar_seconds * Time.deltaTime, max_power));
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
        power = gm.debug_powers ? max_power : value;
        if (on_power_change != null) on_power_change(power);
    }
    private void UpdateRequiredPower(Timeline line, float time)
    {
        req_power = 1;
        foreach (PlayerCmd cmd in line.GetPlayerCmds())
        {
            if (cmd.player_id == player_id)
            {
                float closeness = Mathf.Pow(1f / (Mathf.Abs(cmd.time - time) + 1), 4);
                req_power += closeness * 5f;
            }
        }
    }


    // Events
    private void OnClickAway()
    {
        DeselectPlanet();
    }
    private void OnPlanetClick(Planet planet)
    {
        if (!gm.IsGamePlaying()) return;

        if (selected_planet == null || gm.planet_routes[selected_planet.PlanetID][planet.PlanetID] == null)
        {
            if (planet.OwnerID == player_id || gm.debug_solo)
            {
                // Select planet
                selected_planet = planet;
                selected_planet.ShowHighlight(new Color(0.75f, 0.75f, 0.75f));
            }
        }
        else if (planet != selected_planet)
        {
            if (power >= req_power)
            {
                // Issue player command 
                CmdIssuePlayerCmd(selected_planet.OwnerID, selected_planet.PlanetID,
                    planet.PlanetID, gm.CurrentTimeline.LineID, gm.CurrentTimeline.Time);

                // Cost
                SetPower(power - req_power);

                // UI
                DeselectPlanet();
            }
            
        }
    }
    private void OnPlanetMouseEnter(Planet planet)
    {
        pointed_planet = planet;

        if (!gm.IsGamePlaying()) return;

        // Don't reselect selected planet
        if (planet == selected_planet) return;

        if (selected_planet == null)
        {
            // Highlight planet to select
            planet.ShowHighlight(new Color(0.5f, 0.5f, 0.5f));
        }
        else
        {
            // Highlight planet to target
            if (planet.OwnerID == selected_planet.OwnerID)
                planet.ShowHighlight(Color.green); // Transfer
            else if (gm.planet_routes[selected_planet.PlanetID][planet.PlanetID] != null)
                planet.ShowHighlight(Color.red); // Attack
            else
            {
                //planet.ShowHighlight(new Color(1, 1, 1, 0.5f)); // No action
            }
        }
    }
    private void OnPlanetMouseExit(Planet planet)
    {
        pointed_planet = null;

        if (!gm.IsGamePlaying()) return;

        if (planet != selected_planet)
        {
            // Unhighlight not selected planet
            planet.HideHighlight();
        }
            
    }
    private void OnTimeSet(Timeline line)
    {
        if (!gm.IsGamePlaying()) return;

        // Win condition
        CmdCheckForWin(line.LineID, line.Time);
    }
    private void OnHistoryChange(Timeline line, float earliest)
    {
        if (!ai_controlled) UpdateRequiredPower(gm.CurrentTimeline, gm.CurrentTimeline.Time);
        SetPower(power); // force ui update

        // Win condition
        CmdCheckForWin(gm.CurrentTimeline.LineID, gm.CurrentTimeline.Time);
    }

    // Networking
    [Command]
    private void CmdIssuePlayerCmd(int player_id, int selected_planet_id, int target_planet_id, int line, float time)
    {
        RpcReceivePlayerCmd(player_id, selected_planet_id, target_planet_id, line, time);
    }
    [ClientRpc]
    private void RpcReceivePlayerCmd(int player_id, int selected_planet_id, int target_planet_id, int line, float time)
    {
        PlayerCmd cmd = new PlayerCmd(time, selected_planet_id, target_planet_id, player_id);
        gm.GetTimelines()[line].AddPlayerCmd(cmd);
    }

    [Command]
    private void CmdCheckForWin(int line, float time)
    {
        int winner = gm.GetWinner(line, time); //gm.GetStateWinner(time);
        if (winner >= 0)
        {
            // Win
            Tools.Log("PLAYER " + winner + " WINS!");
            RpcInformWin(winner, line, time);
        }
    }
    [ClientRpc]
    private void RpcInformWin(int winner, int win_line, float win_time)
    {
        gm.OnWin(winner, win_line, win_time);

        DeselectPlanet();
    }
}
