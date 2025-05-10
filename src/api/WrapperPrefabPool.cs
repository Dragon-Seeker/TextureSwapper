using Photon.Pun;
using UnityEngine;

namespace io.wispforest.textureswapper.api;

public class WrapperPrefabPool : IPunPrefabPool {

    public delegate void PostInstantiation(GameObject gameObject, string prefabId, Vector3 position, Quaternion rotation);
    
    public static event PostInstantiation? onPrefabInstantiation;
    
    public IPunPrefabPool pool { get; }
    
    public WrapperPrefabPool(IPunPrefabPool pool) {
        this.pool = pool;
    }

    internal static void attemptToWrapPhotonPool() {
        if (PhotonNetwork.PrefabPool is WrapperPrefabPool) return;
        
        Plugin.Logger.LogInfo("Wrapping Prefab Pool to hook Object Creation");
            
        PhotonNetwork.PrefabPool = new WrapperPrefabPool(PhotonNetwork.PrefabPool);
    }
    
    public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation) {
        var gameObject = pool.Instantiate(prefabId, position, rotation);

        if (gameObject is not null) {
            onPrefabInstantiation?.Invoke(gameObject, prefabId, position, rotation);
        }

        return gameObject;
    }

    public void Destroy(GameObject gameObject) {
        pool.Destroy(gameObject);
    }
}