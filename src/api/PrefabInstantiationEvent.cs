using System.Collections.Generic;
using System.Linq;
using io.wispforest.textureswapper.utils;
using Photon.Pun;
using UnityEngine;

namespace io.wispforest.textureswapper.api;

public class PrefabInstantiationEvent {

    public delegate void PostInstantiation(GameObject gameObject, string prefabId, Vector3 position, Quaternion rotation);
    
    public static event PostInstantiation? onPrefabInstantiation;
    
    public static void onInstantiation(GameObject? gameObject, string prefabId, Vector3 position, Quaternion rotation) {
        if (gameObject is null) return;
        
        onPrefabInstantiation?.Invoke(gameObject, prefabId, position, rotation);
    }
}