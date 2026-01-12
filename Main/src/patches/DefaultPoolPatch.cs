using HarmonyLib;
using io.wispforest.textureswapper.api;
using Photon.Pun;
using UnityEngine;

namespace io.wispforest.textureswapper.patches;

[HarmonyPatch(typeof(DefaultPool))]
public class DefaultPoolPatch {
    [HarmonyPatch(nameof(DefaultPool.Instantiate))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void AwakePatch(string prefabId, Vector3 position, Quaternion rotation, ref GameObject __result) {
        PrefabInstantiationEvent.onInstantiation(__result, prefabId, position, rotation);
    }
}