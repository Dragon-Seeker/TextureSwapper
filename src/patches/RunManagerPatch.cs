using HarmonyLib;
using io.wispforest.textureswapper.api;

namespace io.wispforest.textureswapper.patches;

[HarmonyPatch(typeof(RunManager))]
internal static class RunManagerPatch {
    [HarmonyPatch(nameof(RunManager.Awake))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void AwakePatch() {
        WrapperPrefabPool.attemptToWrapPhotonPool();
    }
}