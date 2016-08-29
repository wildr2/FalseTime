using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class PowerUI : MonoBehaviour
{
    private GameManager gm;
    private PowerBar[] bars;

    private void Awake()
    {
        bars = GetComponentsInChildren<PowerBar>();
        gm = FindObjectOfType<GameManager>();
        gm.on_player_registered += OnPlayerRegistered;
    }
    private void OnPlayerRegistered(Player player)
    {
        if (player.isLocalPlayer)
        {
            player.on_power_change += OnPowerChange;
        }    
    }
    private void OnPowerChange(float power)
    {
        int num_bars = (int)power;
        for (int i = 0; i < bars.Length; ++i)
        {
            bars[bars.Length-1 - i].SetFilled(i < num_bars);
        }
    }
}
