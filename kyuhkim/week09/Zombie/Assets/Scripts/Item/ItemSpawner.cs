using System;
using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public partial class ItemSpawner : ISpawnSender
{
    public void RaiseSpawnEvent(params object[] param)
    {
        var code = (byte)((int)param[1] + 10);
        
        var raiseEventOptions = new RaiseEventOptions
        {
            CachingOption = EventCaching.AddToRoomCacheGlobal,
            Receivers = ReceiverGroup.Others
        };
        
        var sendOptions = new SendOptions
        {
            Reliability = true
        };
        
        // PhotonNetwork.RaiseEvent(CustomEventCode.RequestEvent, param, raiseEventOptions, sendOptions);
        PhotonNetwork.RaiseEvent(code, param, raiseEventOptions, sendOptions);
    }
    
    public void RaiseDespawnEvent(params object[] param)
    {
        var code = (byte)((int)param[1] + 10);
        
        var raiseEventOptions = new RaiseEventOptions
        {
            CachingOption = EventCaching.RemoveFromRoomCache,
            Receivers = ReceiverGroup.Others
        };
        
        var sendOptions = new SendOptions
        {
            Reliability = true
        };

        // PhotonNetwork.RaiseEvent(CustomEventCode.ReleaseEvent, param, raiseEventOptions, sendOptions);
        PhotonNetwork.RaiseEvent(code, param, raiseEventOptions, sendOptions);
    }
}

public partial class ItemSpawner : IOnEventCallback
{
    public void OnEvent(EventData eventData)
    {
        try
        {
            var data = (object[])eventData.CustomData;
            var type = (byte)data[0];
        
            switch (type)
            {
                case CustomEventCode.RequestEvent:
                    EventSpawn(eventData);
                    break;
                case CustomEventCode.ReleaseEvent:
                    EventDespawn(eventData);
                    break;
                default:
                    break;
            }
        }
        catch
        {
        }
    }
}

public partial class ItemSpawner : ISpawnReceiver
{
    public async void EventSpawn(EventData eventData)
    {
        var data = (object[])eventData.CustomData;

        var poolId = (int)data[1];
        var viewId = (int)data[2];
        var position = (Vector3)data[3];
        
        var item = await _poolArray[poolId].RequestBy(viewId);
        item.transform.position = position;
        
        StartCoroutine(DestoryAfter(item, 5f));
    }

    public void EventDespawn(EventData eventData)
    {
        var data = (object[])eventData.CustomData;
        
        var poolId = (int)data[1];
        var viewId = (int)data[2];

        _poolArray[poolId].Release(viewId);
    }
}

public partial class ItemSpawner : MonoBehaviourPun
{
    private static readonly string[] AddressNameArray = new string[] { "AmmoPack", "Coin", "HealthPack" };
    private SyncObjectPool<PhotonView>[] _poolArray;

    private float _maxDistance = 5f;
    private float _timeBetSpawnMax = 7f;
    private float _timeBetSpawnMin = 2f;
    private float _timeBetSpawn;
    private float _lastSpawnTime;

    private void Awake()
    {
        _poolArray = new SyncObjectPool<PhotonView>[AddressNameArray.Length];

        for (var i = 0; i < AddressNameArray.Length; ++i)
        {
            _poolArray[i] = new SyncObjectPool<PhotonView>(AddressNameArray[i]);
        }
    }
    
    private void Start()
    {
        _timeBetSpawn = Random.Range(_timeBetSpawnMin, _timeBetSpawnMax);
        _lastSpawnTime = 0;
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient
            || Time.time < _lastSpawnTime + _timeBetSpawn)
        {
            return;
        }
        
        _lastSpawnTime = Time.time;
        _timeBetSpawn = Random.Range(_timeBetSpawnMin, _timeBetSpawnMax);
        Spawn();
    }
    
    public void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    public void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }
    
    private async void Spawn()
    {
        var position = GetRandomPointOnNavMesh(Vector3.zero, _maxDistance) + Vector3.up * .5f;
        var poolId = Random.Range(0, AddressNameArray.Length);
        var item = await _poolArray[poolId].RequestBy();

        RaiseSpawnEvent(new object[] { CustomEventCode.RequestEvent, poolId, item.ViewID, position });
        item.transform.position = position;
        StartCoroutine(DestoryAfter(item, 5f));
    }

    private IEnumerator DestoryAfter(PhotonView item, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (!item.gameObject.activeInHierarchy || !item.TryGetComponent(out IPooledItem pooledItem))
        {
            yield break;
        }
        
        RaiseDespawnEvent(new object[] { CustomEventCode.ReleaseEvent, ContainerId(item.ViewID), item.ViewID });
        pooledItem.Release();
    }

    private static Vector3 GetRandomPointOnNavMesh(Vector3 center, float distance)
    {
        var randomPos = Random.insideUnitSphere * distance + center;
        NavMesh.SamplePosition(randomPos, out var hit, distance, NavMesh.AllAreas);

        return hit.position;
    }

    private int ContainerId(int viewId)
    {
        for (var id = 0; id < _poolArray.Length; ++id)
        {
            if (_poolArray[id].IsAccounted(viewId))
            {
                return id;
            }
        }

        throw new System.Exception($"wrong ViewID = [{viewId}]");
    }
}

// RPC method 'Restore(Single)' not found on object with PhotonView 3001. Implement as non-static. Apply [PunRPC]. Components on children are not found. Return type must be void or IEnumerator (if you enable RunRpcCoroutines). RPCs are a one-way message.
//     UnityEngine.Debug:LogErrorFormat (UnityEngine.Object,string,object[])
// Photon.Pun.PhotonNetwork:ExecuteRpc (ExitGames.Client.Photon.Hashtable,Photon.Realtime.Player) (at Assets/Photon/PhotonUnityNetworking/Code/PhotonNetworkPart.cs:640)
// Photon.Pun.PhotonNetwork:OnEvent (ExitGames.Client.Photon.EventData) (at Assets/Photon/PhotonUnityNetworking/Code/PhotonNetworkPart.cs:2201)
// Photon.Realtime.LoadBalancingClient:OnEvent (ExitGames.Client.Photon.EventData) (at Assets/Photon/PhotonRealtime/Code/LoadBalancingClient.cs:3353)
// ExitGames.Client.Photon.PeerBase:DeserializeMessageAndCallback (ExitGames.Client.Photon.StreamBuffer) (at D:/Dev/Work/photon-dotnet-sdk/PhotonDotNet/PeerBase.cs:899)
// ExitGames.Client.Photon.EnetPeer:DispatchIncomingCommands () (at D:/Dev/Work/photon-dotnet-sdk/PhotonDotNet/EnetPeer.cs:565)
// ExitGames.Client.Photon.PhotonPeer:DispatchIncomingCommands () (at D:/Dev/Work/photon-dotnet-sdk/PhotonDotNet/PhotonPeer.cs:1771)
// Photon.Pun.PhotonHandler:Dispatch () (at Assets/Photon/PhotonUnityNetworking/Code/PhotonHandler.cs:222)
// Photon.Pun.PhotonHandler:FixedUpdate () (at Assets/Photon/PhotonUnityNetworking/Code/PhotonHandler.cs:145)
