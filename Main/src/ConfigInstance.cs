using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using io.wispforest.textureswapper.api.config;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper;

public class ConfigInstance : LayeredConfigFile {
    
    public readonly Getter<bool> enableDebugLogging;
    public readonly Getter<bool> clientSideMode;
    public readonly Getter<IList<string>> textureSwapTargets;
    public readonly Getter<bool> shouldRestrictQueries;
    
    public readonly Getter<float> tooltipRange;
    public readonly Getter<float> tooltipWaitTime;
    public readonly Getter<bool> showBasicTooltipInfo;
    
    public readonly Getter<int> infoUnpackingAmount;
    public readonly Getter<float> targetDebugRendererLifeSpan;
    public readonly Getter<bool> showTargetDebugRenderer;
    
    public readonly Getter<bool> showDebugInfoInTooltip;
    public readonly Getter<bool> sendTooltipInfoToLog;
    
    public readonly Getter<int> dynamicQueriesLevelCount;
    
    public readonly Getter<bool> onlyFirstAnimationFrame;
    public readonly Getter<bool> allowTranscodingVideos;
    public readonly Getter<bool> prioritizeNewPictures;
    public readonly Getter<bool> prioritizeNewPicturesAcrossLevels;
    
    public readonly Getter<float> minAudioDistance;
    public readonly Getter<float> maxAudioDistance;
    
    public readonly Getter<IList<string>> directoryLocations;
    public readonly Getter<IList<string>> staticWebMedia;
    
    public readonly Getter<IList<string>> blacklistedTags;
    public readonly Getter<bool> enableGlobalBlacklist;
    public readonly Getter<IList<string>> whitelistedTags;
    
    private readonly ConfigFile primaryConfigFile;

    public ConfigInstance(BaseUnityPlugin plugin, ConfigFile primaryConfigFile) : base(plugin) {
        this.primaryConfigFile = primaryConfigFile;
        
        configFileOrder.Add(primaryConfigFile);
        configFileOrder.Add(this);

        var filteredCommaList = Converter.COMMA_SEPARATED_LIST.xmap(input => input.Where(s => !s.IsNullOrWhiteSpace() && s.Length > 0).ToList() as IList<string>, input => input);
        
        //--
        
        primaryConfigFile.section("Common")
            .bind(
                "ClientSideMode", "Enables the ability to use a client based random value that pseudo syncs if the photos are the same on all clients", 
                false, out clientSideMode
            ).bind(
                "DebugLogging", "Enables some useful debug logging to check and or validate if things are going properly", 
                false, out enableDebugLogging
            ).bind(
                "TextureTargets", "All texture targets to replace with custom images, Seperated by commas (,) without any spaces",
                DEFAULT_TEXTURE_TARGETS, out textureSwapTargets, filteredCommaList
            ).bind(
                "RestrictiveQueries", "Will attempt to restrict the queries allowed as an attempt to be safer with image content that is requested", 
                false, out shouldRestrictQueries
            ).bind(
                "DisallowedTags", "A list of tags that are disallowed from being shown, Seperated by commas (,) without any spaces",
                new List<string>(), out blacklistedTags, filteredCommaList
            ).bind(
                "EnableGlobalBlacklist", "Enables the Global Blacklist for generally unsafe queries, disable at your own risk when allowing Restrictive Queries",
                false, out enableGlobalBlacklist
            ).bind(
                "AllowedTags", "A list of tags that are allowed to be shown, Seperated by commas (,) without any spaces",
                new List<string>(), out whitelistedTags, filteredCommaList
            );
        
        //--
        
        primaryConfigFile.section("Tooltip")
            .bind(
                "WaitTime", "Adjust the time between checking for a tooltip object the player is looking at", 
                0.05f, out tooltipWaitTime
            ).bind(
                "Range", "Adjust how far a given tooltip object may be picked up", 
                100f, out tooltipRange
            ).bind(
                "ShowBasicInfoInTooltip", "Adjust if the basic info should show within the tooltip", 
                true, out showBasicTooltipInfo
            );
        
        //--
        
        primaryConfigFile.section("DebugInfo")
            .bind(
                "UnpackingAmount", "Adjust how many parents will be unpacked before getting the info from the given targeted object", 
                1, out infoUnpackingAmount
            ).bind(
                "ShowTargetDebugRenderer", "When enabled, will add a line renderer to the targeted object when ray casting", 
                false, out showTargetDebugRenderer
            ).bind(
                "TargetDebugRendererLifeSpan", "Adjust the amount of time the given targeted objects debug renderer will persist for", 
                6, out targetDebugRendererLifeSpan
            ).bind(
                "ShowDebugInfoInTooltip", "Attempt to show debug info within the tooltip", 
                false, out showDebugInfoInTooltip
            ).bind( 
                "SendTooltipInfoToLog", "Attempt to send tooltip info to log", 
                false, out sendTooltipInfoToLog
            );
        
        //--
        
        primaryConfigFile.section("SwapperSettings")
            .bind(
                "DynamicQueriesLevelCount", "Max levels required to complete before dynamic queries are reloaded for new entries", 
                4, out dynamicQueriesLevelCount
            ).bind(
                "OnlyFirstAnimationFrame", "Only uses the first frame of animation instead of all frames",
                false, out onlyFirstAnimationFrame
            ).bind(
                "AllowTranscodingVideos", "Allows for the ability to transcode video if codec or format is not directly support by unity",
                false, out allowTranscodingVideos
            ).bind(
                "PrioritizeNewPictures", "Attempts to place newer pictures first over already existing pictures",
                true, out prioritizeNewPictures
            ).bind(
                "PrioritizeNewPicturesAcrossLevels", "Transfers PrioritizeNewPictures data across levels to fully  place newer pictures first over already existing pictures",
                true, out prioritizeNewPicturesAcrossLevels
            ).bind(
                "MinAudioDistance", "Minimum distance from the swapped asset in which audio will stay at maximum",
                0.5f, out minAudioDistance
            ).bind(
                "MaxAudioDistance", "Maximum distance from the swapped asset in which audio can be heard",
                6.5f, out maxAudioDistance
            );
        
        //--
        
        primaryConfigFile.section("BuiltinQuerySettings")
            .bind(
                "DirectoryLocations", "Location of all directories to be looked at for images, Seperated by commas (,) without any spaces",
                new List<string>(), out directoryLocations, filteredCommaList
            ).bind(
                "StaticWebMedia", "Location of all photos to be downloaded, Seperated by commas (,) without any spaces", 
                new List<string>(), out staticWebMedia, filteredCommaList
            );
    
        // --

        var tagConvertor = filteredCommaList.xmap(input => new TagFilteringData(input), input => input.tags);
        
        this.section("User Specific")
            .bind(
                "DebugLogging", "Enables some useful debug logging to check and or validate if things are going properly",
                false, builder => builder.onChange(value => UserSettings.dataOrEmpty().debugLogging = value)
            ).bind(
                "RestrictiveQueries", "Will attempt to restrict the queries allowed as an attempt to be safer with image content that is requested",
                true, builder => builder.onChange(value => UserSettings.dataOrEmpty().restrictiveQueries = value)
            ).bind(
                "DisallowedTags", "A list of tags that are disallowed from being shown, Seperated by commas (,) without any spaces",
                new TagFilteringData(), tagConvertor, builder => builder.onChange(value => UserSettings.dataOrEmpty().blackListData = value)
            ).bind(
                "AllowedTags", "A list of tags that are allowed to be shown, Seperated by commas (,) without any spaces",
                new TagFilteringData(), tagConvertor, builder => builder.onChange(value => UserSettings.dataOrEmpty().whiteListData = value)
            );
    }

    public void reloadPrimaryConfig() => primaryConfigFile.Reload();
    
    // Default Target materials
    private static readonly IList<string> DEFAULT_TEXTURE_TARGETS = [
        "\"^(?=.*painting.*)((?!.*frame.*)).*$\"mi",
        "\"^(magazine\\d*) \\(Instance\\)$\"mi",
        "\"(magazine stack)\"mi",
        "\"(Graffiti)\"mi"
    ];
    
    //--
}