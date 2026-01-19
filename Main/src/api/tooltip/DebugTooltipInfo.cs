using System;
using io.wispforest.textureswapper.api.components.holders;
using KeybindLib.Classes;
using REPOLib.Modules;
using UnityEngine;

namespace io.wispforest.textureswapper.utils;

public static class DebugTooltipInfo {
    public static Keybind dumpInfoBind { get; private set; }
    public static Keybind toggleTooltipInfoBind { get; private set; }

    public static void init() {
        dumpInfoBind = Keybinds.Bind("Debug", "Dump Debug Info", "<No Binding>");
        toggleTooltipInfoBind = Keybinds.Bind("Debug", "Toggle Debug Tooltip Info", "<No Binding>");
        
        var rangeKey = ArgKey.optional("range", ArgumentParsers.floatNum(), 50f);
        var unpackAmountKey = ArgKey.optional("unpack", ArgumentParsers.integerNum(), 1);
        
        ArgumentChatCommand.createAndRegister(Plugin.Logger, 
            "rayCastToObj", 
            "Attempts to raycast an object in the scene and dump its structure",
            [rangeKey, unpackAmountKey],
            (_, arguments) => dumpInfo(arguments.get(rangeKey), arguments.get(unpackAmountKey), findNewObj: true)
        );
    }
    
    private static void dumpInfo(float? range = null, int? unpackAmount = null, bool findNewObj = false, Func<String?, bool>? messageHandler = null) {
        var raycastRange = range ?? Plugin.config.tooltipRange();
        var objectUnpackAmount = unpackAmount ?? Plugin.config.infoUnpackingAmount();
        
        var msg = getInfo(raycastRange, objectUnpackAmount, findNewObj);

        if (messageHandler?.Invoke(msg) ?? true) {
            Plugin.Logger.LogInfo($"Dumped Debug Info: \n{msg ?? "No target found!"}");
        }
    }
    
    public static String? getInfo(float range, int unpackAmount, bool findNewObj = false) {
        //Plugin.logIfDebugging(source => source.LogInfo($"RayCasting with at range '{range}'."));

        var rayCaster = MeshRayCaster.getOrCreate();
        
        var target = (findNewObj) ? rayCaster.raycast(range) : rayCaster.currentComponent;
        
        if (target == null) {
            //Plugin.logIfDebugging(source => source.LogInfo("Unable to find any target for raycast!"));
            return null;
        }
        
        var gameObject = target!.transform.gameObject;
        
        // 3. Act on the object hit
        //Plugin.logIfDebugging(source => source.LogInfo($"Primary Target: {gameObject.name}"));
        //Plugin.logIfDebugging(source => source.LogInfo($"Unpacking '{unpackAmount}' levels deep."));
        
        gameObject = gameObject.getParent(unpackAmount);
        
        //Plugin.logIfDebugging(source => source.LogInfo($"Unpacked Target: {gameObject.name}"));

        return gameObject.dumpDebugInfoTree(
                indentSuffix: "  -> ", 
                ignorer: o => o is MeshWireframeRenderer, 
                entryUnpack: (o, list) => {
                    if (o is MeshRenderer renderer) {
                        list.Add(renderer.material);
                        list.addAll(renderer.materials);
                        list.Add(renderer.sharedMaterial);
                        list.addAll(renderer.sharedMaterials);
                    } 
                });
    }

    private static bool toggledTooltipEnabled = Plugin.config.showDebugTooltipInfo();

    private static float countDown = 0;
    
    internal static void update() {
        if (SemiFunc.InputDown(dumpInfoBind.inputKey)) dumpInfo(findNewObj: true);
        if (SemiFunc.InputDown(toggleTooltipInfoBind.inputKey)) toggledTooltipEnabled = !toggledTooltipEnabled;

        if (TooltipUI.DEBUG.predicate.isValid()) {
            if (toggledTooltipEnabled) {
                countDown -= Time.deltaTime;

                if (countDown < 0) {
                    dumpInfo(messageHandler: info => {
                        var tooltipUI = TooltipUI.instance;

                        tooltipUI?.setMessage(TooltipUI.DEBUG, info);
                    
                        // TODO: CONFIG OPTION FOR THIS?
                        return false;
                    });
                
                    countDown = Plugin.config.tooltipWaitTime();
                }
            } else {
                TooltipUI.instance?.removeMessage(TooltipUI.BASIC);
            }
        }
    }
}