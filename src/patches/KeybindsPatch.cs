using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using io.wispforest.textureswapper.utils;
using KeybindLib.Classes;
using Photon.Pun;

namespace io.wispforest.textureswapper.patches;

[HarmonyPatch(typeof(Keybinds))]
public class KeybindsPatch {
    [HarmonyPatch("GetPluginByCallingAssembly")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void AlterAssemblyCheck(Assembly assembly, ref BepInPlugin __result) {
        __result ??= assembly.GetTypes().Select((x => x.GetCustomAttribute<BepInPlugin>())).selectNonNull().FirstOrDefault()
                     ?? assembly.GetExportedTypes().Select((x => x.GetCustomAttribute<BepInPlugin>())).selectNonNull().FirstOrDefault();
    }
}