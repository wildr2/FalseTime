using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

class CustomNetworkManager : NetworkManager
{
    private GameManager gm;
    private int connected_players = 0;

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    {
        if (gm == null) gm = FindObjectOfType<GameManager>();

        Player player = Instantiate(playerPrefab).GetComponent<Player>();
        player.player_id = connected_players;

        player.ai_controlled = connected_players >= gm.num_humans;

        NetworkServer.AddPlayerForConnection(conn, player.gameObject, playerControllerId);

        ++connected_players;

        // Add AI if all humans are connected
        if (connected_players >= gm.num_humans && connected_players < gm.GetNumPlayers())
            ClientScene.AddPlayer((short)(playerControllerId + 1));
    }
    public override void OnStartServer()
    {
        connected_players = 0;
        base.OnStartServer();
    }
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);
        --connected_players;
    }
}