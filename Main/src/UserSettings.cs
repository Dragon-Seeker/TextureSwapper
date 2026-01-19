using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using io.wispforest.endec;
using io.wispforest.endec.format.newtonsoft;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.api;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.VisualScripting;
using JsonSerializer = io.wispforest.endec.format.newtonsoft.JsonSerializer;

namespace io.wispforest.textureswapper;

public static class UserSettings {
    private static bool isDirty = false;
    
    private static readonly System.Collections.Generic.ISet<MediaQueryKey> alternativeCensorImageGroups = new HashSet<MediaQueryKey>();
    private static bool changedAlternativeCensorImages = false;
    
    // TODO: WORK OUT THIS WITHIN THE FUTURE TO POSSIBLY ALLOW OTHER MODS TO ADD ON AND STUFF?
    public static string rawData {
        get {
            reloadData();
            return field;
        }
        private set;
    } = loadUserSettingsData() ?? "{}";
    
    public static SettingsData data {
        get {
            reloadData();
            return field;
        }
        private set {
            if (field?.isPrimarySettings ?? false) field.isPrimarySettings = false;
            
            value.isPrimarySettings = true;
            
            field = value;
        }
    }

    static UserSettings() {
        data = SettingsData.parseOrEmpty(rawData, writeSettings);
    }

    public static Property<T> property<T>(Func<SettingsData, T> getter, Func<SettingsData, T, T> setter) => Property.of(() => getter(data), t => setter(data, t));

    public static string folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "texture_swapper");

    private static string? loadUserSettingsData() {
        var filePath = Path.Combine(folder, "user_settings.json");

        //Plugin.Logger.LogError($"[{filePath}]: ");
        if (!File.Exists(filePath)) return null;
        
        try {
            return File.ReadAllText(filePath);
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to read the texture swapper user settings at [{filePath}]: ");
            Plugin.Logger.LogError(e);
        }

        return null;
    }

    internal static void updateSettings(SettingsData data, Action setCallback) {
        setCallback();
        
        if (!data.isPrimarySettings) return;
        
        writeSettings(rawData, data);
    }

    private static void writeSettings(string rawData, SettingsData data) {
        JToken definedData = new JObject();

        try {
            definedData = SettingsData.ENDEC.encodeFully(JsonSerializer.of, data);
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to encode the texture swapper user settings at due to an exception!");
            Plugin.Logger.LogError(e);
        }
        
        var rawDataObject = new JObject();

        try {
            rawDataObject = JObject.Parse(rawData);
        } catch (Exception _) { }
        
        if (definedData is JObject jObj1) rawDataObject.Merge(jObj1);
        
        var stream = new StringWriter();
        
        rawDataObject.WriteTo(new JsonTextWriter(stream), [ ]);

        var finalData = stream.ToString();
        
        //Plugin.logIfDebugging(() => $"Final save user settings: {finalData}");
        
        Directory.CreateDirectory(folder);
        
        File.WriteAllText(Path.Combine(folder, "user_settings.json"), finalData);
        
        isDirty = true;
    }

    private static void reloadData() {
        if (!isDirty) return;
        
        isDirty = false;

        var prevData = data;
        
        rawData = loadUserSettingsData() ?? "{}";
        data = SettingsData.parseOrEmpty(rawData, writeSettings);

        changedAlternativeCensorImages = prevData.alternativeCensorImages.Equals(data.alternativeCensorImages);
        
        setAltenativeCensorImageGroups(data);
    }
    
    public static ApplicationCredentials? getAppCredentials(string appId, Func<ApplicationCredentials, bool> credentialConfirmationFunc) {
        try {
            var credentials = data.applicationCredentials;
            
            if (credentials.ContainsKey(appId)) {
                var possibleUserCredentials = credentials[appId];
                
                if (possibleUserCredentials is not null && credentialConfirmationFunc(possibleUserCredentials)) return possibleUserCredentials;
            }
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to decode the texture swapper user settings at for App [{appId}]");
            Plugin.Logger.LogError(e);
        }

        return null;
    }
    
    public static bool isAlternativeCensorImage(MediaQueryKey key) => alternativeCensorImageGroups.Contains(key);
    
    private static void setAltenativeCensorImageGroups(SettingsData settingsData) {
        if (!changedAlternativeCensorImages) return;
        
        alternativeCensorImageGroups.Clear();
        alternativeCensorImageGroups.addAll(
            LinqUtility.ToHashSet(
                settingsData
                    .alternativeCensorImages
                    .Values
                    .SelectMany(list => list.Select(query => query.key))
            )
        );
            
        Plugin.runQueries("alternative_censor_images", settingsData.alternativeCensorImages);
            
        changedAlternativeCensorImages = false;
    }

    public static Identifier? getAlternativeCensorImage() {
        if (alternativeCensorImageGroups.Count <= 0) return null;
        
        var entries = MediaSwapperStorage.getMaterials([MediaType.IMAGE, MediaType.VIDEO], id => {
            var key = MediaSwapperStorage.getQueryKey(id);

            return key != null && alternativeCensorImageGroups.Contains(key);
        });

        if (entries.Count <= 0) return null;

        var random = new Random();

        return entries[random.Next(entries.Count)];
    }
    
    public class SettingsData(
        bool debugLogging = false, 
        bool restrictiveQueries = true, 
        bool enableGlobalBlacklist = true,
        TagFilteringData? blackListData = null, 
        TagFilteringData? whiteListData = null, 
        IDictionary<string, ApplicationCredentials>? applicationCredentials = null, 
        IDictionary<Identifier, IList<MediaQuery>>? alternativeCensorImages = null,
        float tooltipRange = 50f,
        float tooltipWaitTime = 0.05f,
        bool showBasicTooltipInfo = true,
        bool showTagsInTooltipInfo = false,
        bool showDescriptionInTooltipInfo = true,
        bool showDebugTooltipInfo = false,
        bool sendTooltipInfoToLog = false
    ) {
        // TODO: MAYBE WE NO LONGER NEED INTERNAL?
        public bool debugLogging { get; internal set => updateSettings(this, () => field = value); } = debugLogging;
        public bool restrictiveQueries { get; internal set => updateSettings(this, () => field = value); } = restrictiveQueries;
        public bool enableGlobalBlacklist { get; internal set => updateSettings(this, () => field = value); } = enableGlobalBlacklist;
        public TagFilteringData blackListData { get; internal set => updateSettings(this, () => field = value); } = blackListData ?? new ();
        public TagFilteringData whiteListData { get; internal set => updateSettings(this, () => field = value); } = whiteListData ?? new ();
        public IDictionary<string, ApplicationCredentials> applicationCredentials { get; internal set => updateSettings(this, () => field = value); } = applicationCredentials ?? new Dictionary<string, ApplicationCredentials>();
        public IDictionary<Identifier, IList<MediaQuery>> alternativeCensorImages { get; internal set => updateSettings(this, () => field = value); } = alternativeCensorImages ?? new Dictionary<Identifier, IList<MediaQuery>>();

        public float tooltipRange { get; internal set => updateSettings(this, () => field = value); } = tooltipRange;
        public float tooltipWaitTime { get; internal set => updateSettings(this, () => field = value); } = tooltipWaitTime;
        public bool showBasicTooltipInfo { get; internal set => updateSettings(this, () => field = value); } = showBasicTooltipInfo;
        public bool showDescriptionInTooltipInfo { get; internal set => updateSettings(this, () => field = value); } = showDescriptionInTooltipInfo;
        public bool showTagsInTooltipInfo { get; internal set => updateSettings(this, () => field = value); } = showTagsInTooltipInfo;
        public bool showDebugTooltipInfo { get; internal set => updateSettings(this, () => field = value); } = showDebugTooltipInfo;
        public bool sendTooltipInfoToLog { get; internal set => updateSettings(this, () => field = value); } = sendTooltipInfoToLog;
        
        //--
        
        internal bool isPrimarySettings = false;

        internal static readonly StructEndec<SettingsData> ENDEC = StructEndecBuilder.of(
            Endec.BOOLEAN.fieldOf<SettingsData>("debugging_logging", s => s.debugLogging),
            Endec.BOOLEAN.fieldOf<SettingsData>("restrictive_queries", s => s.restrictiveQueries),
            Endec.BOOLEAN.fieldOf<SettingsData>("enable_global_blacklist", s => s.enableGlobalBlacklist),
            TagFilteringData.ENDEC.fieldOf<SettingsData>("blacklist", s => s.blackListData),
            TagFilteringData.ENDEC.fieldOf<SettingsData>("whitelist", s => s.whiteListData),
            ApplicationCredentials.ENDEC.mapOf().fieldOf<SettingsData>("account_credentials", s => s.applicationCredentials),
            MediaQueryTypeRegistry.GROUPED_QUERY_DATA.optionalFieldOf<SettingsData>("query_entries", s => s.alternativeCensorImages, () => new Dictionary<Identifier, IList<MediaQuery>>()),
            Endec.FLOAT.fieldOf<SettingsData>("tooltip_range", s => s.tooltipRange),
            Endec.FLOAT.fieldOf<SettingsData>("tooltip_wait_time", s => s.tooltipWaitTime),
            Endec.BOOLEAN.fieldOf<SettingsData>("show_basic_tooltip_info", s => s.showBasicTooltipInfo),
            Endec.BOOLEAN.fieldOf<SettingsData>("show_description_in_tooltip_info", s => s.showDescriptionInTooltipInfo),
            Endec.BOOLEAN.fieldOf<SettingsData>("show_tags_in_tooltip_info", s => s.showTagsInTooltipInfo),
            Endec.BOOLEAN.fieldOf<SettingsData>("show_debug_tooltip_info", s => s.showDebugTooltipInfo),
            Endec.BOOLEAN.fieldOf<SettingsData>("send_tooltip_info_to_log", s => s.sendTooltipInfoToLog),
            (debugLogging, restrictiveQueries, enableGlobalBlacklist, blackListData, whiteListData, applicationCredentials, alternativeCensorImages, tooltipRange, tooltipWaitTime, showBasicTooltipInfo, showDescriptionInTooltipInfo, showTagsInTooltipInfo, showDebugTooltipInfo, sendTooltipInfoToLog) 
                => new SettingsData(debugLogging, restrictiveQueries, enableGlobalBlacklist, blackListData, whiteListData, applicationCredentials, alternativeCensorImages, tooltipRange, tooltipWaitTime, showBasicTooltipInfo, showDescriptionInTooltipInfo, showTagsInTooltipInfo, showDebugTooltipInfo, sendTooltipInfoToLog)
        );

        public static SettingsData parseOrEmpty(string rawData, Action<string, SettingsData> onError) {
            try {
                return JsonUtils.parseFromString(rawData, ENDEC);
            } catch (Exception e) {
                Plugin.Logger.LogError($"Unable to decode the texture swapper user settings as an error occured!");
                Plugin.Logger.LogError(e);
            }
            
            var settings = new SettingsData();
            
            onError(rawData, settings);

            return settings;
        }
    }
}



public class TagFilteringData(IList<string>? tags = null) {

    public IList<string> tags { get; } = tags ?? new List<string>();

    public static readonly StructEndec<TagFilteringData> ENDEC = StructEndecBuilder.of(
        Endec.STRING.listOf().fieldOf<TagFilteringData>("tags", d => d.tags),
        (tags) => new TagFilteringData(tags)
    );
}

public record ApplicationCredentials(string username, string apiKey) {

    public static readonly ApplicationCredentials EMPTY = new ("", "");
    
    public static readonly StructEndec<ApplicationCredentials> ENDEC = StructEndecBuilder.of(
        Endec.STRING.fieldOf<ApplicationCredentials>("username", o => o.username),
        Endec.STRING.fieldOf<ApplicationCredentials>("api_key", o => o.apiKey),
        (username, apiKey) => new ApplicationCredentials(username, apiKey)
    );
    
    public string username { get; internal set; } = username;
    public string apiKey { get; internal set; } = apiKey;
}