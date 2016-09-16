using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class SeedManager : NetworkBehaviour
{
    [SyncVar] public int seed;
    [SyncVar] [NonSerialized] public bool seed_set = false;

    public bool use_custom_seed = true;

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!isServer) return;

        seed = use_custom_seed ? seed : (int)DateTime.Now.Ticks;
        seed_set = true;
    }
}
