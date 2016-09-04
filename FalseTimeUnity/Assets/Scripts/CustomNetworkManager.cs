using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

class CustomNetworkManager : NetworkManager
{
    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
    {
        Player player = Instantiate(playerPrefab).GetComponent<Player>();
        player.player_id = conn.connectionId;

        NetworkServer.AddPlayerForConnection(conn, player.gameObject, playerControllerId);
    }
}