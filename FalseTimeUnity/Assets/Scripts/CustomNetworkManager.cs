using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

class CustomNetworkManager : NetworkManager
{
    private int connected_players = 0;

    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    {
        Player player = Instantiate(playerPrefab).GetComponent<Player>();
        player.player_id = connected_players;

        NetworkServer.AddPlayerForConnection(conn, player.gameObject, playerControllerId);

        ++connected_players;
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