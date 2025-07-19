using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using io.wispforest.impl;
using io.wispforest.textureswapper.endec.format.newtonsoft;
using io.wispforest.textureswapper.utils;
using Newtonsoft.Json.Linq;
using Sirenix.Utilities;
using Unity.VisualScripting;

namespace io.wispforest.textureswapper.api.query.impl;

public class UserSettingsAccess {
    internal static bool isDirty = true;
    
    private static string? userData = loadUserSettingsData();
    private static Settings userSettings = getDefinedSettings();
    
    public static string? getUserSettings() {
        reloadData();
        
        return userData;
    }

    internal static string? loadUserSettingsData() {
        var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var filePath = Path.Combine(folderPath, "texture_swapper_user_settings.json");

        Plugin.Logger.LogError($"[{filePath}]: ");
        if (!File.Exists(filePath)) return null;
        
        try {
            return File.ReadAllText(filePath);
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to read the texture swapper user settings at [{filePath}]: ");
            Plugin.Logger.LogError(e);
        }

        return null;
    }

    internal static Settings updateSettings(Action<Settings> action) {
        Plugin.logIfDebugging(() => $"UPDATING USER SETTINGS");
        var data = userSettings ?? Settings.createEmpty();
        
        action(data);

        JToken json1 = new JObject();

        try {
            json1 = Settings.ENDEC.encodeFully(JsonSerializer.of, data);
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to encode the texture swapper user settings at due to an exception!");
            Plugin.Logger.LogError(e);
        }
        
        var json2 = new JObject();

        try {
            json2 = JObject.Parse(getUserSettings() ?? "{}");

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

        return getDefinedSettings();
    }

    private static void reloadData() {
        if (!isDirty) return;

        var prevSettings = userSettings;
        
        userData = loadUserSettingsData();
        userSettings = userData is not null ? JsonUtils.parseFromString(userData, Settings.ENDEC) : Settings.createEmpty();

        rerunAlternativeCensorImages = prevSettings.alternativeCensorImages.Equals(userSettings.alternativeCensorImages);
        
        setAltenativeCensorImageGroups();
            
        isDirty = false;
    }
    
    public static Settings? getDefinedSettings() {
        reloadData();
        
        return userSettings;
    }
    
    public static ApplicationCredentials? getAppCredentials(string appId, Func<ApplicationCredentials, bool> credentialConfirmationFunc) {
        var data = getDefinedSettings();

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

    private static readonly System.Collections.Generic.ISet<Guid> altenativeCensorImageGroups = new HashSet<Guid>();
    
    public static bool isAlternativeCensorImage(Guid guid) {
        return altenativeCensorImageGroups.Contains(guid);
    }

    private static bool rerunAlternativeCensorImages = false;

    private static void setAltenativeCensorImageGroups() {
        altenativeCensorImageGroups.Clear();
        altenativeCensorImageGroups.AddRange(
                LinqUtility.ToHashSet(
                        getDefinedSettings()
                                .alternativeCensorImages
                                .Values
                                .SelectMany(list => list.Select(query => query.guid))
                )
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
    internal static Settings createEmpty() => new (false, true, new BlackListData([]), new WhiteListData([]), new Dictionary<string, ApplicationCredentials>(), new Dictionary<Identifier, IList<MediaQuery>>());
    
    public bool debugLogging { get; internal set; }
    public bool restrictiveQueries { get; internal set; }
    public BlackListData blackListData { get; internal set; }
    public WhiteListData whiteListData { get; internal set; }
    public IDictionary<string, ApplicationCredentials> applicationCredentials { get; internal set; }
    public IDictionary<Identifier, IList<MediaQuery>> alternativeCensorImages { get; internal set; }

    private Settings(bool debugLogging, bool restrictiveQueries, BlackListData blackListData, WhiteListData whiteListData, IDictionary<string, ApplicationCredentials> applicationCredentials, IDictionary<Identifier, IList<MediaQuery>> alternativeCensorImages) {
        this.debugLogging = debugLogging;
        this.restrictiveQueries = restrictiveQueries;
        this.blackListData = blackListData;
        this.whiteListData = whiteListData;
        this.applicationCredentials = applicationCredentials;
        this.alternativeCensorImages = alternativeCensorImages;
    }

    public static readonly StructEndec<Settings> ENDEC = StructEndecBuilder.of(
        Endecs.BOOLEAN.fieldOf<Settings>("debugging_logging", s => s.debugLogging),
        Endecs.BOOLEAN.fieldOf<Settings>("restrictive_queries", s => s.restrictiveQueries),
        BlackListData.ENDEC.fieldOf<Settings>("blacklist", s => s.blackListData),
        WhiteListData.ENDEC.fieldOf<Settings>("whitelist", s => s.whiteListData),
        ApplicationCredentials.ENDEC.mapOf().fieldOf<Settings>("account_credentials", s => s.applicationCredentials),
        MediaQueryTypeRegistry.GROUPED_QUERY_DATA.optionalFieldOf<Settings>("query_entries", s => s.alternativeCensorImages, () => new Dictionary<Identifier, IList<MediaQuery>>()),
        (debugLogging, restrictiveQueries, blackListData, whiteListData, applicationCredentials, alternativeCensorImages) => new Settings(debugLogging, restrictiveQueries, blackListData, whiteListData, applicationCredentials, alternativeCensorImages)
    );
}

public class BlackListData {

    public IList<string> tags { get; internal set; }
    
    internal BlackListData(IList<string> tags) {
        this.tags = tags;
    }

    public static readonly StructEndec<BlackListData> ENDEC = StructEndecBuilder.of(
        Endecs.STRING.listOf().fieldOf<BlackListData>("user_defined_tags", d => d.tags),
        (tags) => new BlackListData(tags)
    );
}

public class WhiteListData {
    public IList<string> tags { get; internal set; }

    internal WhiteListData(IList<string> tags) {
        this.tags = tags;
    }

    public static readonly StructEndec<WhiteListData> ENDEC = StructEndecBuilder.of(
            Endecs.STRING.listOf().fieldOf<WhiteListData>("user_defined_tags", d => d.tags),
            (tags) => new WhiteListData(tags)
    );
}

public record ApplicationCredentials {

    public static readonly ApplicationCredentials EMPTY = new ("", "");
    
    public static readonly StructEndec<ApplicationCredentials> ENDEC = StructEndecBuilder.of(
        Endecs.STRING.fieldOf<ApplicationCredentials>("username", o => o.username),
        Endecs.STRING.fieldOf<ApplicationCredentials>("api_key", o => o.apiKey),
        (username, apiKey) => new ApplicationCredentials(username, apiKey)
    );
    
    public string username { get; internal set; }
    public string apiKey { get; internal set; }
    
    public ApplicationCredentials(string username, string apiKey) {
        this.username = username;
        this.apiKey = apiKey;
    }
}