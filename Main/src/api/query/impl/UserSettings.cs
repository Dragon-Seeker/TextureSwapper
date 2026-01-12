using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using io.wispforest.endec;
using io.wispforest.endec.format.newtonsoft;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sirenix.Utilities;
using Unity.VisualScripting;
using JsonSerializer = io.wispforest.endec.format.newtonsoft.JsonSerializer;

namespace io.wispforest.textureswapper.api.query.impl;

public static class UserSettings {
    private static bool isDirty = true;
    
    private static readonly System.Collections.Generic.ISet<Guid> alternativeCensorImageGroups = new HashSet<Guid>();
    
    private static bool rerunAlternativeCensorImages = false;

    private static string? userData = loadUserSettingsData();
    private static Settings? userSettings = data();

    public static string? rawData() {
        reloadData();
        
        return userData;
    }
    
    public static Settings? data() {
        reloadData();
        
        return userSettings;
    }
    
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

    internal static void updateSettings(Settings data, Action setCallback) {
        setCallback();
        
        if (!data.setup) return;
        
        JToken json1 = new JObject();

        try {
            json1 = Settings.ENDEC.encodeFully(JsonSerializer.of, data);
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to encode the texture swapper user settings at due to an exception!");
            Plugin.Logger.LogError(e);
        }
        
        var json2 = new JObject();

        try {
            json2 = JObject.Parse(rawData() ?? "{}");

            json2.Remove("blacklist");
            json2.Remove("whitelist");
            json2.Remove("account_credentials");
        } catch (Exception e) { }
        
        if (json1 is JObject jObj1) {
            json2.Merge(jObj1);
        }
        
        
        var stream = new StringWriter();
        
        json2.WriteTo(new JsonTextWriter(stream), [ ]);

        var finalData = stream.ToString();
        
        Plugin.logIfDebugging(() => $"Final save user settings: {finalData}");
        
        var filePath = Path.Combine(folder, "user_settings.json");

        Directory.CreateDirectory(folder);
        
        File.WriteAllText(filePath, finalData);
        
        isDirty = true;
    }

    private static void reloadData() {
        if (!isDirty) return;

        var prevSettings = userSettings;
        
        userData = loadUserSettingsData();
        userSettings = userData is not null ? JsonUtils.parseFromString(userData, Settings.ENDEC) : Settings.createEmpty();

        if (prevSettings is not null) {
            rerunAlternativeCensorImages = prevSettings.alternativeCensorImages.Equals(userSettings.alternativeCensorImages);
        }
        
        setAltenativeCensorImageGroups(userSettings);
            
        isDirty = false;
    }
    
    public static Settings dataOrEmpty() => data() ?? Settings.createEmpty();
    
    public static ApplicationCredentials? getAppCredentials(string appId, Func<ApplicationCredentials, bool> credentialConfirmationFunc) {
        var data = UserSettings.data();

        if (data is not null) {
            try {
                if (data.applicationCredentials.ContainsKey(appId)) {
                    var possibleUserCredentials = data.applicationCredentials[appId];
                
                    if (possibleUserCredentials is not null && credentialConfirmationFunc(possibleUserCredentials)) return possibleUserCredentials;
                }
            } catch (Exception e) {
                Plugin.Logger.LogError($"Unable to decode the texture swapper user settings at for App [{appId}]");
                Plugin.Logger.LogError(e);
            }
        }

        return null;
    }
    
    public static bool isAlternativeCensorImage(Guid guid) => alternativeCensorImageGroups.Contains(guid);
    
    private static void setAltenativeCensorImageGroups(Settings settings) {
        if (settings == null) throw new Exception("FUCCCCCCCCCCCCCCCCCCCCCCCCCCCC");
        if (settings.alternativeCensorImages == null) throw new Exception("SHITTTTTTTTTTTTTTTTTTT");
        if (alternativeCensorImageGroups == null) throw new Exception("CRACKKKKKKKKKERRRRRRRRRRRRRRRRRRSSSSSSSSSSSS");
        
        alternativeCensorImageGroups.Clear();
        alternativeCensorImageGroups.addAll(
            LinqUtility.ToHashSet(
                settings
                    .alternativeCensorImages
                    .Values
                    .SelectMany(list => list.Select(query => query.guid))
            )
        );

        if (rerunAlternativeCensorImages) {
            Plugin.runQueries("alternative_censor_images", settings.alternativeCensorImages);
            
            rerunAlternativeCensorImages = false;
        }
    }

    public static Identifier? getAlternativeCensorImage() {
        if (alternativeCensorImageGroups.Count <= 0) return null;
        
        var entries = MediaSwapperStorage.getMaterials([MediaType.IMAGE, MediaType.VIDEO], (id) => {
            var result = MediaSwapperStorage.getResult(id);
            return result is not null && alternativeCensorImageGroups.Contains(result!.guid);
        });

        if (entries.Count <= 0) return null;

        var random = new Random();

        return entries[random.Next(entries.Count)];
    }
}

public record Settings {
    internal static Settings createEmpty() => new (false, true, new (), new (), new Dictionary<string, ApplicationCredentials>(), new Dictionary<Identifier, IList<MediaQuery>>());

    // TODO: MAYBE WE NO LONGER NEED INTERNAL?
    public bool debugLogging { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public bool restrictiveQueries { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public TagFilteringData blackListData { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public TagFilteringData whiteListData { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public IDictionary<string, ApplicationCredentials> applicationCredentials { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public IDictionary<Identifier, IList<MediaQuery>> alternativeCensorImages { get; internal set => UserSettings.updateSettings(this, () => field = value); }

    public float tooltipRange { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public float tooltipWaitTime { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public bool showBasicTooltipInfo { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public bool showDescriptionInTooltipInfo { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public bool showTagsInTooltipInfo { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public bool showDebugTooltipInfo { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public bool sendTooltipInfoToLog { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    
    internal bool setup = false;

    private Settings(
        bool debugLogging, 
        bool restrictiveQueries, 
        TagFilteringData blackListData, 
        TagFilteringData whiteListData, 
        IDictionary<string, ApplicationCredentials> applicationCredentials, 
        IDictionary<Identifier, IList<MediaQuery>> alternativeCensorImages,
        float tooltipRange = 50f,
        float tooltipWaitTime = 0.05f,
        bool showBasicTooltipInfo = true,
        bool showTagsInTooltipInfo = false,
        bool showDescriptionInTooltipInfo = true,
        bool showDebugTooltipInfo = false,
        bool sendTooltipInfoToLog = false
    ) {
        this.debugLogging = debugLogging;
        this.restrictiveQueries = restrictiveQueries;
        this.blackListData = blackListData;
        this.whiteListData = whiteListData;
        this.applicationCredentials = applicationCredentials;
        this.alternativeCensorImages = alternativeCensorImages;
        this.tooltipRange = tooltipRange;
        this.tooltipWaitTime = tooltipWaitTime;
        this.showBasicTooltipInfo = showBasicTooltipInfo;
        this.showTagsInTooltipInfo = showTagsInTooltipInfo;
        this.showDescriptionInTooltipInfo = showDescriptionInTooltipInfo;
        this.showDebugTooltipInfo = showDebugTooltipInfo;
        this.sendTooltipInfoToLog = sendTooltipInfoToLog;
        setup = true;
    }

    public static readonly StructEndec<Settings> ENDEC = StructEndecBuilder.of(
        Endec.BOOLEAN.fieldOf<Settings>("debugging_logging", s => s.debugLogging),
        Endec.BOOLEAN.fieldOf<Settings>("restrictive_queries", s => s.restrictiveQueries),
        TagFilteringData.ENDEC.fieldOf<Settings>("blacklist", s => s.blackListData),
        TagFilteringData.ENDEC.fieldOf<Settings>("whitelist", s => s.whiteListData),
        ApplicationCredentials.ENDEC.mapOf().fieldOf<Settings>("account_credentials", s => s.applicationCredentials),
        MediaQueryTypeRegistry.GROUPED_QUERY_DATA.optionalFieldOf<Settings>("query_entries", s => s.alternativeCensorImages, () => new Dictionary<Identifier, IList<MediaQuery>>()),
        Endec.FLOAT.fieldOf<Settings>("tooltip_range", s => s.tooltipRange),
        Endec.FLOAT.fieldOf<Settings>("tooltip_wait_time", s => s.tooltipWaitTime),
        Endec.BOOLEAN.fieldOf<Settings>("show_basic_tooltip_info", s => s.showBasicTooltipInfo),
        Endec.BOOLEAN.fieldOf<Settings>("show_description_in_tooltip_info", s => s.showDescriptionInTooltipInfo),
        Endec.BOOLEAN.fieldOf<Settings>("show_tags_in_tooltip_info", s => s.showTagsInTooltipInfo),
        Endec.BOOLEAN.fieldOf<Settings>("show_debug_tooltip_info", s => s.showDebugTooltipInfo),
        Endec.BOOLEAN.fieldOf<Settings>("send_tooltip_info_to_log", s => s.sendTooltipInfoToLog),
        (debugLogging, restrictiveQueries, blackListData, whiteListData, applicationCredentials, alternativeCensorImages, tooltipRange, tooltipWaitTime, showBasicTooltipInfo, showDescriptionInTooltipInfo, showTagsInTooltipInfo, showDebugTooltipInfo, sendTooltipInfoToLog) 
            => new Settings(debugLogging, restrictiveQueries, blackListData, whiteListData, applicationCredentials, alternativeCensorImages, tooltipRange, tooltipWaitTime, showBasicTooltipInfo, showDescriptionInTooltipInfo, showTagsInTooltipInfo, showDebugTooltipInfo, sendTooltipInfoToLog)
    );
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