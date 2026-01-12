using HarmonyLib;

namespace io.wispforest.textureswapper.patches.ui;

[HarmonyPatch(typeof(HealthUI))]
public class HealthUIPatch {
    [HarmonyPatch("Start")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void StartHook(EnergyUI __instance) {
        TooltipUI.setupTooltipUI(__instance);
    }
}