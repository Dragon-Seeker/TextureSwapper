using System;
using System.Collections.Generic;
using System.IO;
using io.wispforest.impl;
using io.wispforest.textureswapper.endec.format.newtonsoft;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.api.query.impl;

public class TagMangement {
    
    public static IList<string> USER_GLOBAL_BLACKLIST = getUserGlobalBlacklist();
    
    public static IList<string> getUserGlobalBlacklist() {
        var data = UserSettingsAccess.getUserSettings();

        if (data is not null) {
            try {
                var endec = Endecs.STRING.listOf()
                        .structOf("blacklist")
                        .structOf("user_defined_tags");
                    
                return JsonUtils.parseFromString(data, endec);
            } catch (Exception e) {
                Plugin.Logger.LogError($"Unable to decode the texture swapper user settings to get global whitelist tags.");
                Plugin.Logger.LogError(e);
            }
        }
        
        return [];
    }

    public static IList<string> USER_GLOBAL_WHITELIST = getUserGlobalWhitelist();
    
    public static IList<string> getUserGlobalWhitelist() {
        var data = UserSettingsAccess.getUserSettings();

        if (data is not null) {
            try {
                var endec = Endecs.STRING.listOf()
                        .structOf("whitelist")
                        .structOf("user_defined_tags");
                    
                return JsonUtils.parseFromString(data, endec);
            } catch (Exception e) {
                Plugin.Logger.LogError($"Unable to decode the texture swapper user settings to get global whitelist tags.");
                Plugin.Logger.LogError(e);
            }
        }
        
        return [];
    }
    
    public static IList<string> getBlackListTags(bool authorizedUser, List<string> extraBlackList) {
        var configBlackListTags = new List<string>(Plugin.ConfigAccess.blackListTags);
        

        if (!authorizedUser || Plugin.ConfigAccess.enableGlobalBlacklist()) {
            configBlackListTags.AddRange(extraBlackList);
        }
        
        configBlackListTags.RemoveAll(Plugin.ConfigAccess.whiteListTags.Contains);

        configBlackListTags.AddRange(USER_GLOBAL_BLACKLIST);
        configBlackListTags.RemoveAll(USER_GLOBAL_WHITELIST.Contains);

        return configBlackListTags;
    }
}