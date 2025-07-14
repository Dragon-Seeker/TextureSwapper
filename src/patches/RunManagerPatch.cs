using HarmonyLib;
using io.wispforest.textureswapper.api;

namespace io.wispforest.textureswapper.patches;

[HarmonyPatch(typeof(RunManager))]
internal static class RunManagerPatch {
    [HarmonyPatch("RestartScene")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void RestartScenePatch() {
        var manager = RunManager.instance;
        LevelEvents.onLevelChange(manager.levelsCompleted, manager.levelCurrent.name);
    }
}