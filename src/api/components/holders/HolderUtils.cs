using System;
using UnityEngine;

namespace io.wispforest.textureswapper.api.components.holders;

public class HolderUtils {
    
    public static T getOrCreate<T>(ref GameObject? holderObj, Action resetAction, string name) where T : MonoEvent {
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

            holderObj.AddComponent<T>().onDestoryCallback += _ => resetAction();
        }
        
        return holderObj.GetComponent<T>();
    }
}