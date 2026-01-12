using System;
using System.Linq;
using System.Text;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.utils;
using KeybindLib.Classes;
using UnityEngine;

namespace io.wispforest.textureswapper.api.tooltip;

public class BasicTooltipInfo {
    
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
        Plugin.logIfDebugging(source => source.LogInfo($"RayCasting with at range '{range}'."));

        var rayCaster = MeshRayCaster.getOrCreate();
        
        var target = (findNewObj) ? rayCaster.raycast(range) : rayCaster.currentComponent;
        
        if (target == null) {
            Plugin.logIfDebugging(source => source.LogInfo("Unable to find any target for raycast!"));
            return null;
        }
        
        var gameObject = target!.transform.gameObject;
        
        // 3. Act on the object hit
        Plugin.logIfDebugging(source => source.LogInfo($"Primary Target: {gameObject.name}"));
        Plugin.logIfDebugging(source => source.LogInfo($"Unpacking '{unpackAmount}' levels deep."));
        
        gameObject = gameObject.getParent(unpackAmount);
        
        Plugin.logIfDebugging(source => source.LogInfo($"Unpacked Target: {gameObject.name}"));

        var holder = gameObject.GetComponent<SwapperHandlerHolder>();
        
        if (holder != null) {
            var builder = new StringBuilder();
            
            Plugin.logIfDebugging(source => source.LogInfo($"SwapperHandlerHolder was found!"));
            
            var swapperData = holder.getAppliedSwapIds()
                    .Select(MediaSwapperStorage.getFullData)
                    .ToList();
            
            Plugin.logIfDebugging(source => source.LogInfo($"Size of active swaps: {swapperData.Count}"));
            
            for (var i = 0; i < swapperData.Count; i++) {
                var data = swapperData[i];

                if (swapperData.Count != 1) {
                    builder.AppendLine($"Swap [{i}]: ");
                }
                
                builder.AppendLine($"    Id: {data.id}");
                builder.AppendLine($"Status: {MediaSwapperStorage.getMediaState(data.id)}");
                builder.AppendLine($"Format: {data.info.format.name()}");
                builder.AppendLine("-------------------");

                data.result.addToTooltip(builder);

                if (findNewObj && data.result is MediaPostResult postResult) {
                    GUIUtility.systemCopyBuffer = postResult.postUrl;
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }
        
        return null;
    }

    private static bool toggledTooltipEnabled = false;
    
    private static bool tooltipEnabled => toggledTooltipEnabled || Plugin.config.showBasicTooltipInfo();

    private static float countDown = 0;
    
    internal static void update() {
        if (SemiFunc.InputDown(dumpInfoBind.inputKey)) dumpInfo(findNewObj: true);
        if (SemiFunc.InputDown(toggleTooltipInfoBind.inputKey)) toggledTooltipEnabled = !toggledTooltipEnabled;

        if (TooltipUI.BASIC.predicate.isValid() && tooltipEnabled) {
            countDown -= Time.deltaTime;

            if (countDown < 0) {
                dumpInfo(messageHandler: info => {
                    var tooltipUI = TooltipUI.instance;

                    tooltipUI?.setMessage(TooltipUI.BASIC, info);
                    
                    // TODO: CONFIG OPTION FOR THIS?
                    return false;
                });
                
                countDown = Plugin.config.tooltipWaitTime();
            }
        }
    }
}