using HarmonyLib;
using io.wispforest.textureswapper.api;
using REPOLib.Modules;

namespace io.wispforest.textureswapper.patches;

internal class RepoLibNetworkPrefabsPatch {
    [HarmonyPatch(typeof(NetworkPrefabs), "Initialize")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void AwakePatch() {
        WrapperPrefabPool.attemptToWrapPhotonPool();
    }
}