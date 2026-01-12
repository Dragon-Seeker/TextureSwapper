using HarmonyLib;

namespace io.wispforest.textureswapper.patches.ui;

[HarmonyPatch(typeof(EnergyUI))]
public class EnergyUIPatch {
    [HarmonyPatch("Start")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void StartHook(EnergyUI __instance) {
        TooltipUI.setupTooltipUI(__instance);
    }
}