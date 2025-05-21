using System;
using System.IO;
using io.wispforest.impl;
using io.wispforest.textureswapper.endec.format.newtonsoft;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper.api.query.impl;

public class UserSettingsAccess {
    public static Endec<(string username, string apiKey)> createEndec(string appId) {
        return StructEndecBuilder.of(
                        Endecs.STRING.fieldOf<(string username, string apiKey)>("username", o => o.username),
                        Endecs.STRING.fieldOf<(string username, string apiKey)>("api_key", o => o.apiKey),
                        (username, apiKey) => new (username, apiKey)
                ).structOf($"{appId}_credentials")
                .structOf("account_credentials");
    }

    public static string? getUserSettings() {
        var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var filePath = Path.Combine(folderPath, "texture_swapper_account_credentials.json");

        if (!File.Exists(filePath)) return null;
        
        try {
            return File.ReadAllText(filePath);
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to read the texture swapper user settings at [{filePath}]: ");
            Plugin.Logger.LogError(e);
        }

        return null;
    }
    
    public static (string username, string apiKey)? getAppCredentials(string appId, Func<(string username, string apiKey), bool> credentialConfirmationFunc) {
        var data = getUserSettings();

        if (data is not null) {
            try {
                var possibleUserCredentials = JsonUtils.parseFromString(data, createEndec(appId));
                
                if (credentialConfirmationFunc(possibleUserCredentials)) return possibleUserCredentials;
            } catch (Exception e) {
                Plugin.Logger.LogError($"Unable to decode the texture swapper user settings at for App [{appId}]");
                Plugin.Logger.LogError(e);
            }
        }

        return null;
    }
}