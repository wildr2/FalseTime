using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class PowerUI : MonoBehaviour
{
    private GameManager gm;
    private PowerBar bar;
    private Player player;

    private void Awake()
    {
        bar = GetComponent<PowerBar>();
        gm = FindObjectOfType<GameManager>();
        gm.on_player_registered += OnPlayerRegistered;
    }
    private void OnPlayerRegistered(Player player)
    {
        if (player.isLocalPlayer && !player.ai_controlled)
        {
            player.on_power_change += OnPowerChange;
            this.player = player;
        }    
    }
    private void OnPowerChange(float power)
    {
        bar.SetFill(power / player.GetPowerMax());
        bar.SetFillGoal(player.GetPowerReq() / player.GetPowerMax());
    }
}
