using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class Player : NetworkBehaviour
{
    // General
    [SyncVar] public int player_id; // assume arbitrary numbers
    private bool initialized = false;

    // References
    private GameManager gm;

    // Selection
    private Planet pointed_planet;
    private Planet selected_planet;

    // Power
    private float power_bar_seconds = 15;
    private int max_power = 4;
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
        gm.on_history_change += OnHistoryChange;
    }

    private IEnumerator Initialize()
    {
        while (!gm.IsInitialized()) yield return null;

        if (isLocalPlayer)
        {
            gm.GetTimeline().on_time_set += OnTimeSet;

            foreach (Planet planet in gm.GetPlanets())
            {
                // Planet mouse events
                planet.on_pointer_enter += OnPlanetMouseEnter;
                planet.on_pointer_exit += OnPlanetMouseExit;
            }

            StartCoroutine(HumanUpdate());
        }

        gm.RegisterPlayer(this);
        if (gm.debug_solo)
        {
            ++player_id;
            gm.RegisterPlayer(this);
            --player_id;
        }
        
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

            // Power Growth
            SetPower(Mathf.Min(power + 1f / power_bar_seconds * Time.deltaTime, max_power));


            yield return null;
        }
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
    private void UpdateRequiredPower()
    {
        req_power = 1;
        foreach (PlayerCmd cmd in gm.GetPlayerCmds())
        {
            float closeness = Mathf.Pow(1f / (Mathf.Abs(cmd.time - gm.GetTimeline().Time) + 1), 4);
            req_power += closeness * 5f;
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

        if (selected_planet == null)
        {
            if (planet.OwnerID == player_id || gm.debug_solo)
            {
                // Select planet
                selected_planet = planet;
                selected_planet.ShowHighlight(Color.white);
            }
        }
        else if (planet != selected_planet)
        {
            if (power >= req_power)
            {
                // Issue player command 
                CmdIssuePlayerCmd(selected_planet.OwnerID, selected_planet.PlanetID, planet.PlanetID, gm.GetTimeline().Time);

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
            planet.ShowHighlight(new Color(1, 1, 1, 0.5f));
        }
        else
        {
            // Highlight planet to target
            if (planet.OwnerID == selected_planet.OwnerID)
                planet.ShowHighlight(Color.green); // Friendly
            else
                planet.ShowHighlight(Color.red); // Enemy
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
    private void OnTimeSet(float time)
    {
        if (!gm.IsGamePlaying()) return;

        // Cmd cost
        UpdateRequiredPower();
        SetPower(power); // force ui update

        // Win condition
        CmdCheckForWin(time);
    }
    private void OnHistoryChange()
    {
        UpdateRequiredPower();
        SetPower(power); // force ui update

        // Win condition
        CmdCheckForWin(gm.GetTimeline().Time);
    }

    // Networking
    [Command]
    private void CmdIssuePlayerCmd(int player_id, int selected_planet_id, int target_planet_id, float time)
    {
        RpcReceivePlayerCmd(player_id, selected_planet_id, target_planet_id, time);
    }
    [ClientRpc]
    private void RpcReceivePlayerCmd(int player_id, int selected_planet_id, int target_planet_id, float time)
    {
        PlayerCmd cmd = new PlayerCmd(time, selected_planet_id, target_planet_id, player_id);
        gm.AddPlayerCmd(cmd);
    }

    [Command]
    private void CmdCheckForWin(float time)
    {
        int winner = gm.GetWinner(); //gm.GetStateWinner(time);
        if (winner >= 0)
        {
            // Win
            Tools.Log("PLAYER " + winner + " WINS!");
            RpcInformWin(winner, time);
        }
    }
    [ClientRpc]
    private void RpcInformWin(int winner, float win_time)
    {
        gm.OnWin(winner, win_time);

        DeselectPlanet();
    }
}
