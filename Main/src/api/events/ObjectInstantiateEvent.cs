using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace io.wispforest.textureswapper.utils;

// TODO: STILL NOT FULLY WORKING FOR ALL CASES AND IGNORES LEVEL LOADING SADLY
public class ObjectInstantiateEvent {
    public delegate void PostInstantiation(Object obj, Vector3? position, Quaternion? rotation);
    
    public static event PostInstantiation? onObjectInstantiation;

    public static void setupAndRun(object __result, object[]? __args = null) {
        if (__result is not Object obj) return;
        
        //Plugin.Logger.LogInfo($"Name: {obj.name}, Type: {obj.GetType()}");
        
        try {
            Vector3? position = null;
            Quaternion? rotation = null;

            if (__args != null) {
                foreach (var arg in __args) {
                    if (arg is Vector3 vec) position = vec;
                    else if (arg is Quaternion quat) rotation = quat;
                }
            }

            onInstantiation(obj, position, rotation);
        } catch (Exception e) {
            Plugin.Logger.LogError(e);
        }
    }
    
    public static void onInstantiation(Object obj, Vector3? position, Quaternion? rotation) {
        onObjectInstantiation?.Invoke(obj, position, rotation);
    }
}