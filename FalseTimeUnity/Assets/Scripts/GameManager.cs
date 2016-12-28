using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // General
    private bool game_over = false;

    // Players
    public Player[] Players { get; private set; } // keys are player ids
    private int players_registered = 0;
    private int[] player_scores; // flag counts

    // References
    public Transform connection_screen;
    
    // Events
    public System.Action<Player> on_player_registered;
    public System.Action on_all_players_registered;
    public System.Action<int> on_player_win; // player id
    public System.Action<int> on_player_score; // player id



    // PUBLIC ACCESSORS

    public bool ArePlayersRegistered()
    {
        return players_registered == DataManager.Instance.GetNumPlayers();
    }
    public bool IsGameOver()
    {
        return game_over;
    }
    public bool IsGamePlaying()
    {
        return ArePlayersRegistered() && !IsGameOver();
    }
   
    public Player GetLocalHumanPlayer()
    {
        foreach (Player p in Players)
        {
            if (p.isLocalPlayer && !p.AI) return p;
        }
        return null;
    }

    public int GetPlayerScore(Player player)
    {   
        return player_scores[player.PlayerID];
    }
    public int GetPlayerScore(int player_id)
    {
        return player_scores[player_id];
    }
    public int GetWinner()
    {
        // Win by having high enough score
        for (int i = 0; i < Players.Length; ++i)
        {
            if (player_scores[i] >= DataManager.Instance.flags_to_win)
                return i;
        }

        return -1;
    }


    // PUBLIC MODIFIERS

    public void RegisterPlayer(Player player)
    {
        Players[player.PlayerID] = player;
        ++players_registered;

        Tools.Log("Registered player " + player.PlayerID
            + (player.AI ? " (AI)" : ""), Color.blue);

        // Events
        if (on_player_registered != null) on_player_registered(player);
        if (ArePlayersRegistered())
        {
            // All Players registered
            if (on_all_players_registered != null)
                on_all_players_registered();
            connection_screen.gameObject.SetActive(false);
        }
    }
    public void OnWin(int winner)
    {
        game_over = true;

        if (on_player_win != null) on_player_win(winner);
    }


    // PRIVATE MODIFIERS / HELPERS

    private void Awake()
    {
        FindObjectOfType<Metaverse>().on_new_flag += OnNewFlag;

        // Players
        Players = new Player[DataManager.Instance.GetNumPlayers()];
        player_scores = new int[Players.Length];
    }
    private void OnNewFlag(NewFlagEvent e)
    {
        player_scores[e.player_id] += 1;
        if (on_player_score != null) on_player_score(e.player_id);
    }

}