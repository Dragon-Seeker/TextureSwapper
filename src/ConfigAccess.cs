using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using HarmonyLib;
using io.wispforest.textureswapper.api.components;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.utils;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace io.wispforest.textureswapper;

public class ConfigAccess : ConfigFile, IDictionary<ConfigDefinition, ConfigEntryBase> {
    
    private readonly ConfigEntry<bool> DEBUG_LOGGING;
    private readonly ConfigEntry<bool> CLIENT_SIDE_ONLY;
    private readonly ConfigEntry<String> PICTURE_TEXTURE_TARGETS;
    private readonly ConfigEntry<bool> RESTRICTED_QUERIES;
    
    private readonly ConfigEntry<bool> ONLY_FIRST_ANIMATION_FRAME;
    private readonly ConfigEntry<bool> ALLOW_TRANSCODING_VIDEOS;
    private readonly ConfigEntry<bool> PRIORITIZE_NEW_PICTURES;
    
    private readonly ConfigEntry<float> MIN_AUDIO_DISTANCE;
    private readonly ConfigEntry<float> MAX_AUDIO_DISTANCE;
    
    private readonly ConfigEntry<String> DIRECTORY_LOCATION;
    private readonly ConfigEntry<String> STATIC_WEB_MEDIA;
    
    private readonly ConfigEntry<String> BLACKLIST_TAGS;
    private readonly ConfigEntry<bool> ENABLE_GLOBAL_BLACKLIST;
    private readonly ConfigEntry<String> ALLOWED_TAGS;
    
    private readonly ConfigFile primaryConfigFile;

    public ConfigAccess(ConfigFile primaryConfigFile, BepInPlugin ownerMetadata) : base(Utility.CombinePaths(Paths.ConfigPath, ownerMetadata.GUID + "_user_settings" + ".cfg"), false, ownerMetadata) {
        this.primaryConfigFile = primaryConfigFile;

        this.SaveOnConfigSet = false;
        
        //--
        
        primaryConfigFile.Section("Common")
                .Bind(out CLIENT_SIDE_ONLY, "ClientSide", false, "Enables the ability to use a client based random value that pseudo syncs if the photos are the same on all clients")
                .Bind(out DEBUG_LOGGING, "DebugLogging", false, "Enables some useful debug logging to check and or validate if things are going properly")
                .Bind(out PICTURE_TEXTURE_TARGETS, "TextureTargets", DEFAULT_TEXTURE_TARGETS, "All texture targets to replace with custom images, Seperated by commas (,) without any spaces")
                .Bind(out RESTRICTED_QUERIES, "RestrictiveQueries", true, "Will attempt to restrict the queries allowed as an attempt to be safer with image content that is requested")
                .Bind(out BLACKLIST_TAGS, "DisallowedTags", "", "A list of tags that are disallowed from being shown, Seperated by commas (,) without any spaces")
                .Bind(out ENABLE_GLOBAL_BLACKLIST, "EnableGlobalBlacklist", false, "Enables the Global Blacklist for generally unsafe queries, disable at your own risk when allowing Restrictive Queries")
                .Bind(out ALLOWED_TAGS, "AllowedTags", "", "A list of tags that are allowed to be shown, Seperated by commas (,) without any spaces");

        //--
        
        primaryConfigFile.Section("SwapperSettings")
                .Bind(out ONLY_FIRST_ANIMATION_FRAME, "OnlyFirstAnimationFrame", false, "Only uses the first frame of animation instead of all frames")
                .Bind(out ALLOW_TRANSCODING_VIDEOS, "AllowTranscodingVideos", false, "Allows for the ability to transcode video if codec or format is not directly support by unity")
                .Bind(out PRIORITIZE_NEW_PICTURES, "PrioritizeNewPictures", true, "Attempts to place newer pictures first over already existing pictures")
                .Bind(out MIN_AUDIO_DISTANCE, "MinAudioDistance", 0.5f, "Minimum distance from the swapped asset in which audio will stay at maximum")
                .Bind(out MAX_AUDIO_DISTANCE, "MaxAudioDistance", 6.5f, "Maximum distance from the swapped asset in which audio can be heard");
        
        //--
        
        primaryConfigFile.Section("BuiltinQuerySettings")
                .Bind(out DIRECTORY_LOCATION, "DirectoryLocations", "", "Location of all directories to be looked at for images, Seperated by commas (,) without any spaces")
                .Bind(out STATIC_WEB_MEDIA, "StaticWebMedia", "", "Location of all photos to be downloaded, Seperated by commas (,) without any spaces");
    
        // --

        var currentSettings = UserSettingsAccess.getDefinedSettings()!;
        
        this.Section("User Specific")
                .Bind("DebugLogging", currentSettings.debugLogging, false, "Enables some useful debug logging to check and or validate if things are going properly", 
                        newValue => UserSettingsAccess.updateSettings(settings => settings.debugLogging = newValue).debugLogging)
                
                .Bind("RestrictiveQueries", currentSettings.restrictiveQueries, true, "Will attempt to restrict the queries allowed as an attempt to be safer with image content that is requested", 
                        newValue => UserSettingsAccess.updateSettings(settings => settings.restrictiveQueries = newValue).restrictiveQueries)
                
                .Bind("DisallowedTags", currentSettings.blackListData.tags.Join(delimiter: ","), "", "A list of tags that are disallowed from being shown, Seperated by commas (,) without any spaces", 
                        newValue => UserSettingsAccess.updateSettings(settings => settings.blackListData.tags = newValue.Split(",").ToList()).blackListData.tags.Join(delimiter: ","))
                
                .Bind("AllowedTags", currentSettings.whiteListData.tags.Join(delimiter: ","), "", "A list of tags that are allowed to be shown, Seperated by commas (,) without any spaces", 
                        newValue => UserSettingsAccess.updateSettings(settings => settings.whiteListData.tags = newValue.Split(",").ToList()).whiteListData.tags.Join(delimiter: ","));

        
        var isDebugRefreshPresent = debugLogging() && clientSideOnly();
        
        // TODO: REMOVE LATER?
        this.primaryConfigFile.SaveOnConfigSet = !(isDebugRefreshPresent);
        
        if (isDebugRefreshPresent) {
            var CLIENT_SIDE_ONLY_REFRESH = primaryConfigFile.Bind("Common", "ClientSideRefresh", false, new ConfigDescription("Useful feature to try and reload stuff after changes to config for Client Side only stuff"));

            CLIENT_SIDE_ONLY_REFRESH.SettingChanged += (sender, args) => {
                primaryConfigFile.Reload();
            
                Scene activeScene = SceneManager.GetActiveScene();
            
                GameObject[] rootObjects = activeScene.GetRootGameObjects();

                // Iterate through the root GameObjects and print their names.
                foreach (GameObject rootObject in rootObjects) {
                    if (!rootObject.name.Equals("Level Generator")) continue;
                
                    SwapperComponentSetupUtils.unswapScene(rootObject);
                
                    ActiveSwapperHolder.getOrCreate().reset();
                
                    SwapperComponentSetupUtils.commonSide(rootObject);
                }
            };
        }
        
        primaryConfigFile.SettingChanged += (_, _) => setupValues();
        
        setupValues();
    }

    private void setupValues() {
        pictureTextureTargets = parseString(PICTURE_TEXTURE_TARGETS.Value);
        directoryLocations = parseString(DIRECTORY_LOCATION.Value);
        staticWebMedia = parseString(STATIC_WEB_MEDIA.Value);
        blackListTags = parseString(BLACKLIST_TAGS.Value);
        whiteListTags = parseString(ALLOWED_TAGS.Value);
    }
    
    public bool debugLogging() => DEBUG_LOGGING.Value;
    public bool clientSideOnly() => CLIENT_SIDE_ONLY.Value;
    public List<string> pictureTextureTargets { get; private set; }
    public bool restrictiveQueries() => RESTRICTED_QUERIES.Value;
    public bool enableGlobalBlacklist() => ENABLE_GLOBAL_BLACKLIST.Value;
    
    public bool onlyFirstAnimationFrame() => ONLY_FIRST_ANIMATION_FRAME.Value;
    public bool allowTranscodingVideos() => ALLOW_TRANSCODING_VIDEOS.Value;
    public bool prioritizeNewPictures() => PRIORITIZE_NEW_PICTURES.Value;
    public float minAudioDistance() => MIN_AUDIO_DISTANCE.Value;
    public float maxAudioDistance() => MAX_AUDIO_DISTANCE.Value;
    
    public List<string> directoryLocations { get; private set; }
    public List<string> staticWebMedia { get; private set; }
    
    public List<string> blackListTags { get; private set; }
    public List<string> whiteListTags { get; private set; }

    private static List<string> parseString(String value) {
        return value.Split(',').Where(s => !s.IsNullOrWhiteSpace() && s.Length > 0).ToList();
    }
    
    // Default Target materials
    private static readonly string DEFAULT_TEXTURE_TARGETS = 
    new List<string>([
            "\"^(?=.*painting.*)((?!.*frame.*)).*$\"mi",
            "\"^(magazine\\d*) \\(Instance\\)$\"mi",
            "\"(magazine stack)\"mi"
    ]).Join(delimiter: ",");
    
    //--
    
    public new IEnumerator<KeyValuePair<ConfigDefinition, ConfigEntryBase>> GetEnumerator() {
        return new IEnumeratorIEnumerator<KeyValuePair<ConfigDefinition, ConfigEntryBase>>(
                [primaryConfigFile.GetEnumerator(), base.GetEnumerator()]
        );
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
    
    public bool Contains(KeyValuePair<ConfigDefinition, ConfigEntryBase> item) {
        return base.Contains(item) || primaryConfigFile.Contains(item);
    }

    public int Count => base.Count + primaryConfigFile.Count;

    public bool ContainsKey(ConfigDefinition key) {
        return base.ContainsKey(key) || primaryConfigFile.ContainsKey(key);
    }

    public bool Remove(ConfigDefinition key) {
        if (base.Remove(key)) return true;
        if (primaryConfigFile.Remove(key)) return true;
        
        return false;
    }

    public void Clear() {
        base.Clear();
        primaryConfigFile.Clear();
    }
    
    ConfigEntryBase IDictionary<ConfigDefinition, ConfigEntryBase>.this[ConfigDefinition key] {
        get {
            return (primaryConfigFile.ContainsKey(key)) 
                    ? primaryConfigFile[key] 
                    : base[key];
        } 
        set { throw new InvalidOperationException("Directly setting a config entry is not supported"); }
    }

    public ConfigEntryBase this[ConfigDefinition key] { 
        get {
            return (primaryConfigFile.ContainsKey(key)) 
                    ? primaryConfigFile[key] 
                    : base[key];
        }
    }

    public ICollection<ConfigDefinition> Keys { 
        get { 
            var keys = new List<ConfigDefinition>(primaryConfigFile.Keys); 
            
            keys.AddRange(base.Keys); 
            
            return keys; 
        }
    }
}

public class SectionBinder(ConfigFile configFile, string section) {
    
    public SectionBinder Bind<T>(out ConfigEntry<T> field, string key, T defaultValue, ConfigDescription? configDescription = null) {
        field = configFile.Bind(section, key, defaultValue, configDescription);

        return this;
    }

    public SectionBinder Bind<T>(out ConfigEntry<T> field, string key, T defaultValue, string description) {
        field = configFile.Bind(section, key, defaultValue, description);
        
        return this;
    }
    
    public SectionBinder Bind<T>(string key, T defaultValue, T baseValue, string description, Func<T, T> onChangeCallback) {
        bool isLocked = false;
                
        var entry = configFile.Bind(section, key, defaultValue, description);

        entry.Value = baseValue;
                
        entry.SettingChanged += (sender, _) => {
            if (isLocked) return;
            Plugin.logIfDebugging(() => $"{key} value has been updated! Type: [{sender.GetType().Name}]");
            if (sender is ConfigEntry<T> e) {
                isLocked = true;
                e.Value = onChangeCallback(e.Value);
                isLocked = false;
            }
        };
        
        return this;
    }
}

public static class ConfigFileExtensions {
    public static SectionBinder Section(this ConfigFile file, string section) {
        return new SectionBinder(file, section);
    }
}

public class IEnumeratorIEnumerator<T> : IEnumerator<T> {
    private readonly List<IEnumerator<T>> enumerators;

    public IEnumeratorIEnumerator(List<IEnumerator<T>> enumerators) {
        this.enumerators = enumerators;
    }
    
    private int index;
    private IEnumerator<T>? currentEnumerator;

    private bool canMoveNext() {
        return (index < enumerators.Count);
    }

    private bool? canMoveNextInner = null;

    private bool safeMoveNext() {
        if (canMoveNextInner is null) {
            canMoveNextInner = currentEnumerator.MoveNext();
        }
        
        return canMoveNextInner ?? false;
    }
    
    private bool setup() {
        if (currentEnumerator is null || !safeMoveNext()) {
            if (!canMoveNext()) return false;
        
            currentEnumerator = enumerators[index];

            canMoveNextInner = null;

            safeMoveNext();

            index++;
        }

        return true;
    }
    
    public bool MoveNext() {
        while (setup()) {
            if (canMoveNextInner ?? false) return true;
        }

        return false;
    }

    public void Reset() {
        foreach (var enumerator in enumerators) {
            enumerator.Reset();
        }

        index = 0;
    }

    object? IEnumerator.Current => Current;

    public T? Current {
        get {
            var result = setup() ? currentEnumerator!.Current : default;
            
            canMoveNextInner = null;
            
            return result;
        }
    }

    public void Dispose() {
        foreach (var enumerator in enumerators) {
            enumerator.Dispose();
        }
    }
}