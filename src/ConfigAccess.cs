using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using HarmonyLib;
using io.wispforest.textureswapper.api.components;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.utils;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace io.wispforest.textureswapper;

public class ConfigAccess {
    
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
    
    private readonly ConfigFile configFile;

    public ConfigAccess(ConfigFile configFile) {
        this.configFile = configFile;
        
        //--
        
        configFile.Section("Common")
                .Bind(out CLIENT_SIDE_ONLY, "ClientSide", false, "Enables the ability to use a client based random value that pseudo syncs if the photos are the same on all clients")
                .Bind(out DEBUG_LOGGING, "DebugLogging", false, "Enables some useful debug logging to check and or validate if things are going properly")
                .Bind(out PICTURE_TEXTURE_TARGETS, "TextureTargets", DEFAULT_TEXTURE_TARGETS, "All texture targets to replace with custom images, Seperated by commas (,) without any spaces")
                .Bind(out RESTRICTED_QUERIES, "RestrictiveQueries", true, "Will prevent restrict the queries allow to be safer with image content requested")
                .Bind(out BLACKLIST_TAGS, "DisallowedTags", "", "A list of tags that are disallowed from being shown, Seperated by commas (,) without any spaces");
        //--
        
        configFile.Section("SwapperSettings")
                .Bind(out ONLY_FIRST_ANIMATION_FRAME, "OnlyFirstAnimationFrame", false, "Only uses the first frame of animation instead of all frames")
                .Bind(out ALLOW_TRANSCODING_VIDEOS, "AllowTranscodingVideos", false, "Allows for the ability to transcode video if codec or format is not directly support by unity")
                .Bind(out PRIORITIZE_NEW_PICTURES, "PrioritizeNewPictures", true, "Attempts to place newer pictures first over already existing pictures")
                .Bind(out MIN_AUDIO_DISTANCE, "MinAudioDistance", 0.5f, "Minimum distance from the swapped asset in which audio will stay at maximum")
                .Bind(out MAX_AUDIO_DISTANCE, "MaxAudioDistance", 6.5f, "Maximum distance from the swapped asset in which audio can be heard");
        
        //--
        
        configFile.Section("BuiltinQuerySettings")
                .Bind(out DIRECTORY_LOCATION, "DirectoryLocations", "", "Location of all directories to be looked at for images, Seperated by commas (,) without any spaces")
                .Bind(out STATIC_WEB_MEDIA, "StaticWebMedia", "", "Location of all photos to be downloaded, Seperated by commas (,) without any spaces");
    
        // --
        
        var isDebugRefreshPresent = debugLogging() && clientSideOnly();
        
        // TODO: REMOVE LATER?
        this.configFile.SaveOnConfigSet = !(isDebugRefreshPresent);
        
        if (isDebugRefreshPresent) {
            var CLIENT_SIDE_ONLY_REFRESH = configFile.Bind("Common", "ClientSideRefresh", false, new ConfigDescription("Useful feature to try and reload stuff after changes to config for Client Side only stuff"));

            CLIENT_SIDE_ONLY_REFRESH.SettingChanged += (sender, args) => {
                configFile.Reload();
            
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
        
        configFile.SettingChanged += (_, _) => setupValues();
        
        setupValues();
    }

    private void setupValues() {
        pictureTextureTargets = parseString(PICTURE_TEXTURE_TARGETS.Value);
        directoryLocations = parseString(DIRECTORY_LOCATION.Value);
        staticWebMedia = parseString(STATIC_WEB_MEDIA.Value);
        blackListTags = parseString(BLACKLIST_TAGS.Value);
    }
    
    public bool debugLogging() => DEBUG_LOGGING.Value;
    public bool clientSideOnly() => CLIENT_SIDE_ONLY.Value;
    public List<string> pictureTextureTargets { get; private set; }
    public bool restrictiveQueries() => RESTRICTED_QUERIES.Value;
    
    public bool onlyFirstAnimationFrame() => ONLY_FIRST_ANIMATION_FRAME.Value;
    public bool allowTranscodingVideos() => ALLOW_TRANSCODING_VIDEOS.Value;
    public bool prioritizeNewPictures() => PRIORITIZE_NEW_PICTURES.Value;
    public float minAudioDistance() => MIN_AUDIO_DISTANCE.Value;
    public float maxAudioDistance() => MAX_AUDIO_DISTANCE.Value;
    
    public List<string> directoryLocations { get; private set; }
    public List<string> staticWebMedia { get; private set; }
    
    public List<string> blackListTags { get; private set; }

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
}

public class SectionBinder(ConfigFile configFile, string section) {
    
    public SectionBinder Bind<T>(out ConfigEntry<T> field, string key, T defaultValue, ConfigDescription configDescription = null) {
        field = configFile.Bind(section, key, defaultValue, configDescription);

        return this;
    }

    public SectionBinder Bind<T>(out ConfigEntry<T> field, string key, T defaultValue, string description) {
        field = configFile.Bind(section, key, defaultValue, description);
        
        return this;
    }
}

public static class ConfigFileExtensions {
    public static SectionBinder Section(this ConfigFile file, string section) {
        return new SectionBinder(file, section);
    }
}