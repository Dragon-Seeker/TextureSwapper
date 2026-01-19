using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using io.wispforest.textureswapper.api.components;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.utils;
using KeybindLib.Classes;
using UnityEngine;

namespace io.wispforest.textureswapper.api.tooltip;

public abstract class BasicTooltipInfo {
    
    public static Keybind dumpInfoBind { get; private set; }
    public static Keybind toggleTooltipInfoBind { get; private set; }
    
    public static void init() {
        dumpInfoBind = Keybinds.Bind("Tooltip", "Dump Basic Info", "<Keyboard>/o");
        toggleTooltipInfoBind = Keybinds.Bind("Tooltip", "Toggle Basic Tooltip Info", "<Keyboard>/l");
    }
    
    private static void dumpInfo(float? range = null, int? unpackAmount = null, bool findNewObj = false, Func<String?, bool>? messageHandler = null) {
        var raycastRange = range ?? Plugin.config.tooltipRange();
        var objectUnpackAmount = unpackAmount ?? 0;
        
        var msg = getInfo(raycastRange, objectUnpackAmount, findNewObj);

        if (messageHandler?.Invoke(msg) ?? true) {
            Plugin.Logger.LogInfo($"Dumped Basic Info: \n{msg ?? "No target found!"}");
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

        var swapperData = new List<FullMediaData>();

        var idComponent = gameObject.GetComponent<MediaIdentifierComponent>();

        if (idComponent != null) {
            swapperData.Add(MediaSwapperStorage.getFullData(idComponent.getId()));
        }
        
        // var holder = gameObject.GetComponent<SwapperHandlerHolder>();
        //
        // if (holder != null) {
        //     foreach (var fullMediaData in holder.getAppliedSwapIds().Select(MediaSwapperStorage.getFullData)) {
        //         if (!swapperData.Contains(fullMediaData)) swapperData.Add(fullMediaData);
        //     }
        // }
        
        if (swapperData.isNotEmpty()) {
            var builder = new StringBuilder();
            
            for (var i = 0; i < swapperData.Count; i++) {
                var data = swapperData[i];
                
                if (Plugin.isFunnyPerson && Equals(data.id, Plugin.funnyId)) continue;
                
                if (swapperData.Count != 1) {
                    builder.AppendLine($"Swap [{i}]: ");
                }

                appendData(builder, data);

                if (findNewObj && data.result is MediaPostResult postResult) {
                    GUIUtility.systemCopyBuffer = postResult.postUrl;
                }

                builder.AppendLine();
            }

            var str = builder.ToString();
            
            return string.IsNullOrWhiteSpace(str) ? null : str;
        }
        
        return null;
    }

    public static void appendData(StringBuilder builder, FullMediaData data) {
        if (Plugin.isFunnyPerson && Equals(data.id, Plugin.funnyId)) return;
                
        builder.AppendLine($"    Id: {data.id}");
        builder.AppendLine($"Status: {MediaSwapperStorage.getMediaState(data.id)}");
        builder.AppendLine($"Format: {data.info.format.name()}");
        builder.AppendLine("-------------------");

        data.result.addToTooltip(builder);

        builder.AppendLine();
    }

    private static bool toggledTooltipEnabled = Plugin.config.showBasicTooltipInfo();
    
    internal static void update() {
        if (SemiFunc.InputDown(dumpInfoBind.inputKey)) dumpInfo(findNewObj: true);
        if (SemiFunc.InputDown(toggleTooltipInfoBind.inputKey)) toggledTooltipEnabled = !toggledTooltipEnabled;

        if (TooltipUI.BASIC.predicate.isValid()) {
            if (toggledTooltipEnabled) {
                dumpInfo(messageHandler: info => {
                    var tooltipUI = TooltipUI.instance;

                    tooltipUI?.setMessage(TooltipUI.BASIC, info);
                    
                    // TODO: CONFIG OPTION FOR THIS?
                    return false;
                });
            } else {
                TooltipUI.instance?.removeMessage(TooltipUI.BASIC);
            }
        }
    }
}