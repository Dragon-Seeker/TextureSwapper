using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using io.wispforest.endec;
using io.wispforest.endec.format.newtonsoft;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.utils;
using Newtonsoft.Json.Linq;
using Sirenix.Utilities;
using Unity.VisualScripting;

namespace io.wispforest.textureswapper.api.query.impl;

public class UserSettings {
    internal static bool isDirty = true;
    
    private static string? userData = loadUserSettingsData();
    private static Settings userSettings = data();
    
    public static string? rawData() {
        reloadData();
        
        return userData;
    }

    internal static string? loadUserSettingsData() {
        var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var filePath = Path.Combine(folderPath, "texture_swapper_user_settings.json");

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

        var finalData = JsonUtils.writeToString(json2);
        
        Plugin.logIfDebugging(() => $"Final save user settings: {finalData}");
        
        var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var filePath = Path.Combine(folderPath, "texture_swapper_user_settings.json");
        
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
        setAltenativeCensorImageGroups();
            
        isDirty = false;
    }
    
    public static Settings? data() {
        reloadData();
        
        return userSettings;
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

    private static System.Collections.Generic.ISet<Guid> altenativeCensorImageGroups = new HashSet<Guid>();
    
    public static bool isAlternativeCensorImage(Guid guid) {
        return altenativeCensorImageGroups.Contains(guid);
    }

    private static bool rerunAlternativeCensorImages = false;

    private static void setAltenativeCensorImageGroups() {
        altenativeCensorImageGroups = LinqUtility.ToHashSet(
                userSettings
                        .alternativeCensorImages
                        .Values
                        .SelectMany(list => list.Select(query => query.guid))
        );

        if (rerunAlternativeCensorImages) {
            Plugin.runQueries("alternative_censor_images", userSettings.alternativeCensorImages);
            
            rerunAlternativeCensorImages = false;
        }
    }

    public static Identifier? getAlternativeCensorImage() {
        if (altenativeCensorImageGroups.Count <= 0) return null;
        
        var entries = MediaSwapperStorage.getMaterials([MediaType.IMAGE, MediaType.VIDEO], (id) => {
            var result = MediaSwapperStorage.getResult(id);
            return result is not null && altenativeCensorImageGroups.Contains(result!.guid);
        });

        if (entries.Count <= 0) return null;

        var random = new Random();

        return entries[random.Next(entries.Count)];
    }
}

public record Settings {
    internal static Settings createEmpty() => new (false, true, new TagFilteringData(), new TagFilteringData(), new Dictionary<string, ApplicationCredentials>(), new Dictionary<Identifier, IList<MediaQuery>>());

    // TODO: MAYBE WE NO LONGER NEED INTERNAL?
    public bool debugLogging { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public bool restrictiveQueries { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public TagFilteringData blackListData { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public TagFilteringData whiteListData { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public IDictionary<string, ApplicationCredentials> applicationCredentials { get; internal set => UserSettings.updateSettings(this, () => field = value); }
    public IDictionary<Identifier, IList<MediaQuery>> alternativeCensorImages { get; internal set => UserSettings.updateSettings(this, () => field = value); }

    private Settings(bool debugLogging, bool restrictiveQueries, TagFilteringData blackListData, TagFilteringData whiteListData, IDictionary<string, ApplicationCredentials> applicationCredentials, IDictionary<Identifier, IList<MediaQuery>> alternativeCensorImages) {
        this.debugLogging = debugLogging;
        this.restrictiveQueries = restrictiveQueries;
        this.blackListData = blackListData;
        this.whiteListData = whiteListData;
        this.applicationCredentials = applicationCredentials;
        this.alternativeCensorImages = alternativeCensorImages;
    }

    public static readonly StructEndec<Settings> ENDEC = StructEndecBuilder.of(
        endec.Endec.BOOLEAN.fieldOf<Settings>("debugging_logging", s => s.debugLogging),
        endec.Endec.BOOLEAN.fieldOf<Settings>("restrictive_queries", s => s.restrictiveQueries),
        TagFilteringData.ENDEC.fieldOf<Settings>("blacklist", s => s.blackListData),
        TagFilteringData.ENDEC.fieldOf<Settings>("whitelist", s => s.whiteListData),
        ApplicationCredentials.ENDEC.mapOf().fieldOf<Settings>("account_credentials", s => s.applicationCredentials),
        MediaQueryTypeRegistry.GROUPED_QUERY_DATA.optionalFieldOf<Settings>("query_entries", s => s.alternativeCensorImages, () => new Dictionary<Identifier, IList<MediaQuery>>()),
        (debugLogging, restrictiveQueries, blackListData, whiteListData, applicationCredentials, alternativeCensorImages) => new Settings(debugLogging, restrictiveQueries, blackListData, whiteListData, applicationCredentials, alternativeCensorImages)
    );
}

public class TagFilteringData(IList<string>? tags = null) {

    public TagFilteringData(string data) : this(data.Split(",").ToList()) { }

    public IList<string> tags { get; internal set; } = tags ?? new List<string>();

    public static readonly StructEndec<TagFilteringData> ENDEC = StructEndecBuilder.of(
        Endec.STRING.listOf().fieldOf<TagFilteringData>("user_defined_tags", d => d.tags),
        (tags) => new TagFilteringData(tags)
    );

    public string tagsAsString() => tags.Join(delimiter: ",");
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