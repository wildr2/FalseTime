using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;


public class DataManager : MonoBehaviour
{
    private static DataManager _instance;
    public static DataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = GameObject.FindObjectOfType<DataManager>();

                if (_instance == null) Debug.LogError("Missing DataManager");
                else
                {
                    DontDestroyOnLoad(_instance);
                    _instance.Initialize();
                }
            }
            return _instance;
        }
    }

    // Debug
    public bool debug_solo = false;
    public bool debug_powers = false;
    public bool debug_log_tl_remake = false;

    // Match type
    public int num_universes = 2;
    public int num_planets = 15;
    public int flags_to_win = 25;

    // Players
    public int num_humans = 2;
    public int num_bots = 0;
    public Color[] color_options;
    public int[] player_color_ids;
    public bool random_colors = false;
    public string[] color_names;

    // Key Colors
    public Color color_enemy;
    public Color color_friendly;
    public Color color_timetravel;


    // PUBLIC ACCESSORS

    public bool ValidColorChoices()
    {
        for (int i = 0; i < GetNumPlayers(); ++i)
        {
            for (int j = 0; j < GetNumPlayers(); ++j)
            {
                if (player_color_ids[i] == player_color_ids[j]) return false;
            }
        }
        return true;
    }

    public int GetNumPlayers()
    {
        return num_humans + num_bots;
    }
    public Color GetPlayerColor(int id)
    {
        return color_options[player_color_ids[id]];
    }
    public string GetPlayerColorName(int id)
    {
        return color_names[player_color_ids[id]];
    }


    // PUBLIC MODIFIERS


    // PRIVATE / PROTECTED MODIFIERS

    private void Awake()
    {
        // if this is the first instance, make this the singleton
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(_instance);
            Initialize();
        }
        else
        {
            // destroy other instances that are not the already existing singleton
            if (this != _instance)
                Destroy(this.gameObject);
        }
    }
    private void Initialize()
    {
        if (random_colors)
        {
            player_color_ids = Tools.ShuffleArray(
               Enumerable.Range(0, color_options.Length).ToArray());
        }
    }

}
