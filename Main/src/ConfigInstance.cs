using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using io.wispforest.textureswapper.api.config;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper;

public class ConfigInstance : LayeredConfigFile {

    public readonly Getter<bool> enableDebugLogging;
    public readonly Getter<bool> clientSideMode;
    public readonly Getter<bool> shouldRestrictQueries;

    public readonly Getter<float> tooltipRange;
    public readonly Getter<float> tooltipWaitTime;
    public readonly Getter<bool> showBasicTooltipInfo;
    public readonly Getter<bool> showDescriptionInTooltipInfo;
    public readonly Getter<bool> showTagsInTooltipInfo;
    public readonly Getter<bool> showDebugTooltipInfo;
    public readonly Getter<bool> sendTooltipInfoToLog;
    
    public readonly Getter<int> infoUnpackingAmount;
    public readonly Getter<float> targetDebugRendererLifeSpan;
    public readonly Getter<bool> showTargetDebugRenderer;
    
    public readonly Getter<int> dynamicQueriesLevelCount;
    
    public readonly Getter<bool> onlyFirstAnimationFrame;
    public readonly Getter<bool> allowTranscodingVideos;
    public readonly Getter<bool> prioritizeNewPictures;
    public readonly Getter<bool> prioritizeNewPicturesAcrossLevels;
    
    public readonly Getter<float> minAudioDistance;
    public readonly Getter<float> maxAudioDistance;
    
    public readonly Getter<IList<string>> directoryLocations;
    public readonly Getter<IList<string>> staticWebMedia;
    public readonly Getter<bool> funny;
    
    public readonly Getter<IList<string>> blacklistedTags;
    public readonly Getter<bool> enableGlobalBlacklist;
    public readonly Getter<IList<string>> whitelistedTags;
    
    private readonly ConfigFile primaryConfigFile;
    private readonly ConfigFile userSettingsConfigFile;

    public ConfigInstance(BaseUnityPlugin plugin, ConfigFile primaryConfigFile) : base(plugin) {
        this.primaryConfigFile = primaryConfigFile;
        this.userSettingsConfigFile = new DummyConfigFile("user_settings", MetadataHelper.GetMetadata(plugin));

        var filteredCommaList = Converter.COMMA_SEPARATED_LIST.xmap(input => input.Where(s => !s.IsNullOrWhiteSpace() && s.Length > 0).ToList() as IList<string>, input => input);
        
        System.Converter<TagFilteringData, IList<string>> tagDataUnpacker = input => input.tags;
        var tagConvertor = filteredCommaList.xmap(input => new TagFilteringData(input), tagDataUnpacker);
        
        // --
        
        section(userSettingsConfigFile, "User Specific")
            .bind(
                "DebugLogging", "Enables some useful debug logging to check and or validate if things are going properly",
                UserSettings.property((data) => data.debugLogging, (data, v) => data.debugLogging = v).setGetter(out enableDebugLogging)
            ).bind(
                "RestrictiveQueries", "Will attempt to restrict the queries allowed as an attempt to be safer with image content that is requested",
                UserSettings.property((data) => data.restrictiveQueries, (data, v) => data.restrictiveQueries = v).setGetter(out shouldRestrictQueries)
            ).bind(
                "EnableGlobalBlacklist", "Enables the Global Blacklist for generally unsafe queries, disable at your own risk when allowing Restrictive Queries",
                UserSettings.property((data) => data.enableGlobalBlacklist, (data, v) => data.enableGlobalBlacklist = v).setGetter(out enableGlobalBlacklist)
            ).bind(
                "DisallowedTags", "A list of tags that are disallowed from being shown, Seperated by commas (,) without any spaces",
                UserSettings.property((data) => data.blackListData, (data, v) => data.blackListData = v).setGetter(out blacklistedTags, tagDataUnpacker), tagConvertor
            ).bind(
                "AllowedTags", "A list of tags that are allowed to be shown, Seperated by commas (,) without any spaces",
                UserSettings.property((data) => data.whiteListData, (data, v) => data.whiteListData = v).setGetter(out whitelistedTags, tagDataUnpacker), tagConvertor
            );
        
        // --
        
        section(userSettingsConfigFile, "Tooltip")
            .bind<float>(
                "WaitTime", "Adjust the time between checking for a tooltip object the player is looking at", 
                UserSettings.property((data) => data.tooltipWaitTime, (data, v) => data.tooltipWaitTime = v).setGetter(out tooltipWaitTime),
                builder => builder.valuePredicate(new AcceptableValueRange<float>(0, 30f))
            ).bind<float>(
                "Range", "Adjust how far a given tooltip object may be picked up", 
                UserSettings.property((data) => data.tooltipRange, (data, v) => data.tooltipRange = v).setGetter(out tooltipRange),
                builder => builder.valuePredicate(new AcceptableValueRange<float>(0, 250f))
            ).bind(
                "ShowBasicInfoInTooltip", "Adjust if the basic info should show within the tooltip", 
                UserSettings.property((data) => data.showBasicTooltipInfo, (data, v) => data.showBasicTooltipInfo = v).setGetter(out showBasicTooltipInfo)
            ).bind(
                "ShowDebugInfoInTooltip", "Toggles if debug info will show within tooltip info", 
                UserSettings.property((data) => data.showDebugTooltipInfo, (data, v) => data.showDebugTooltipInfo = v).setGetter(out showDebugTooltipInfo)
            ).bind( 
                "ShowTagsInTooltipInfo", "Toggles if tag information will show up in tooltip info", 
                UserSettings.property((data) => data.showTagsInTooltipInfo, (data, v) => data.showTagsInTooltipInfo = v).setGetter(out showTagsInTooltipInfo)
            ).bind( 
                "ShowDescriptionInTooltipInfo", "Toggles if description information will show up in tooltip info", 
                UserSettings.property((data) => data.showDescriptionInTooltipInfo, (data, v) => data.showDescriptionInTooltipInfo = v).setGetter(out showDescriptionInTooltipInfo)
            ).bind( 
                "SendTooltipInfoToLog", "Attempt to send tooltip info to log", 
                UserSettings.property((data) => data.sendTooltipInfoToLog, (data, v) => data.sendTooltipInfoToLog = v).setGetter(out sendTooltipInfoToLog)
            );
        
        // --
        
        section(primaryConfigFile, "Modpack Settings")
            .bind(
                "ClientSideMode", "Enables the ability to use a client based random value that pseudo syncs if the photos are the same on all clients", 
                false, out clientSideMode
            ).bind(
                "DisallowedTags", "A list of tags that are disallowed from being shown, Seperated by commas (,) without any spaces",
                new List<string>(), out blacklistedTags, filteredCommaList
            ).bind(
                "AllowedTags", "A list of tags that are allowed to be shown, Seperated by commas (,) without any spaces",
                new List<string>(), out whitelistedTags, filteredCommaList
            ).bind(
                "DirectoryLocations", "Location of all directories to be looked at for images, Seperated by commas (,) without any spaces",
                new List<string>(), out directoryLocations, filteredCommaList
            ).bind(
                "StaticWebMedia", "Location of all media to be downloaded, Seperated by commas (,) without any spaces", 
                new List<string>(), out staticWebMedia, filteredCommaList
            ).bind("Funny", "Its funny", true, out funny);
        
        // --
        
        section(primaryConfigFile, "Swapper Settings")
            .bind(
                "PrioritizeNewPictures", "Attempts to place newer pictures first over already existing pictures",
                true, out prioritizeNewPictures
            ).bind(
                "PrioritizeNewPicturesAcrossLevels", "Transfers PrioritizeNewPictures data across levels to fully  place newer pictures first over already existing pictures",
                true, out prioritizeNewPicturesAcrossLevels
            );
        
        // --
        
        section(primaryConfigFile, "Media Settings")
            .bind(
                "DynamicQueriesLevelCount", "Max levels required to complete before dynamic queries are reloaded for new entries", 
                4, out dynamicQueriesLevelCount,
                builder => builder.valuePredicate(new AcceptableValueRange<int>(0, int.MaxValue))
            ).bind(
                "OnlyFirstAnimationFrame", "Only uses the first frame of animation instead of all frames",
                false, out onlyFirstAnimationFrame
            ).bind(
                "AllowTranscodingVideos", "Allows for the ability to transcode video if codec or format is not directly support by unity",
                false, out allowTranscodingVideos
            ).bind(
                "MinAudioDistance", "Minimum distance from the swapped asset in which audio will stay at maximum",
                0.5f, out minAudioDistance,
                    builder => builder.valuePredicate(new AcceptableValueRange<float>(0, float.MaxValue))
            ).bind(
                "MaxAudioDistance", "Maximum distance from the swapped asset in which audio can be heard",
                6.5f, out maxAudioDistance,
                builder => builder.valuePredicate(new AcceptableValueRange<float>(0, float.MaxValue)) // TODO: USE MIN TO SET MINIUM MAX VALUE
            );
        
        // --
        
        section(primaryConfigFile, "Debug Info")
            .bind(
                "UnpackingAmount", "Adjust how many parents will be unpacked before getting the info from the given targeted object", 
                0, out infoUnpackingAmount,
                builder => builder.valuePredicate(new AcceptableValueRange<int>(0, 3))
            ).bind(
                "ShowTargetDebugRenderer", "When enabled, will add a line renderer to the targeted object when ray casting", 
                false, out showTargetDebugRenderer
            ).bind(
                "TargetDebugRendererLifeSpan", "Adjust the amount of time the given targeted objects debug renderer will persist for", 
                6, out targetDebugRendererLifeSpan,
                builder => builder.valuePredicate(new AcceptableValueRange<float>(0, float.MaxValue))
            );
    }

    public void reloadPrimaryConfig() => primaryConfigFile.Reload();
}

public static class PropertyExt {
    public static Property<T> setGetter<T>(this Property<T> property, out Getter<T> getter) {
        getter = property.asGetter();
        
        return property;
    }
    
    public static Property<T> setGetter<T, R>(this Property<T> property, out Getter<R> getter, System.Converter<T, R> converter) {
        getter = () => converter(property.get());
        
        return property;
    }
    
    public static Getter<T> asGetter<T>(this Property<T> property) => property.get;
}