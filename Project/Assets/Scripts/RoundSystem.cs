using Mirror;
using System.Linq;
using UnityEngine;

public class RoundSystem : NetworkBehaviour
{
    [SerializeField] private Animator animator = null;

    public Rigidbody rbPlayer;

    private NetworkManagerReligion room;
    private NetworkManagerReligion Room
    {
        get
        {
            if (room != null) { return room; }
            return room = NetworkManager.singleton as NetworkManagerReligion;
        }
    }

    public void CountdownEnded()
    {
        animator.enabled = false;
    }

    #region Server

    public override void OnStartServer()
    {
        NetworkManagerReligion.OnServerStopped += CleanUpServer;
        NetworkManagerReligion.OnServerReadied += CheckToStartRound;
    }

    [ServerCallback]
    private void OnDestroy() => CleanUpServer();

    [Server]
    private void CleanUpServer()
    {
        NetworkManagerReligion.OnServerStopped -= CleanUpServer;
        NetworkManagerReligion.OnServerReadied -= CheckToStartRound;
    }

    [ServerCallback]
    public void StartRound()
    {
        RpcStartRound();
    }

    [Server]
    private void CheckToStartRound(NetworkConnection conn)
    {
        if (Room.GamePlayers.Count(x => x.connectionToClient.isReady) != Room.GamePlayers.Count) { return; }

        animator.enabled = true;

        RpcStartCountdown();
    }
    #endregion

    #region Client

    [ClientRpc]
    private void RpcStartCountdown()
    {
        animator.enabled = true;
    }

    [ClientRpc]
    private void RpcStartRound()
    {
      
    }
        
    

    #endregion
}
