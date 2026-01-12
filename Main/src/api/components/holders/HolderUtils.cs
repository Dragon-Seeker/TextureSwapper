using System;
using UnityEngine;

namespace io.wispforest.textureswapper.api.components.holders;

public class HolderUtils {
    
    public static T getOrCreate<T>(ref GameObject? holderObj, Action resetAction, string name, Action<T>? onCreation = null) where T : MonoEvent {
        if (!Plugin.isMainThread()) {
            throw new Exception($"Unable to create Holder Object and its component as its not on the main thread! [Name:{name}]");
        }
        
        if (holderObj is null) {
            holderObj = new GameObject {
                    name = name,
                    transform = {
                            position = Vector3.zero,
                            rotation = Quaternion.identity,
                            localScale = Vector3.one
                    }
            };

            Plugin.logIfDebugging(source => source.LogInfo($"Empty GameObject created: {name}"));

            var t = holderObj.AddComponent<T>();
            
            t.onDestoryCallback += _ => resetAction();

            onCreation?.Invoke(t);
        }
        
        return holderObj.GetComponent<T>();
    }
}