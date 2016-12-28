using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

class CustomNetworkManager : NetworkManager
{
    private int connected_players = 0;

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    {
        DataManager dm = DataManager.Instance;

        Player player = Instantiate(playerPrefab).GetComponent<Player>();
        player.PlayerID = connected_players;

        player.AI = connected_players >= dm.num_humans;

        NetworkServer.AddPlayerForConnection(conn, player.gameObject, playerControllerId);

        ++connected_players;

        // Add AI if all humans are connected
        if (connected_players >= dm.num_humans && connected_players < dm.GetNumPlayers())
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