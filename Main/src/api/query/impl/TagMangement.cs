using System;
using System.Collections.Generic;
using System.IO;
using io.wispforest.endec.impl;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.api.query.impl;

public class TagMangement {
    
    public static IList<string> getUserGlobalBlacklist() {
        var tags = UserSettings.data?.blackListData.tags;

        if (tags is not null) return tags;
        
        Plugin.Logger.LogError($"Unable to decode the texture swapper user settings to get global blacklist tags.");

        return [];

    }
    
    public static IList<string> getUserGlobalWhitelist() {
        var tags = UserSettings.data?.whiteListData.tags;

        if (tags is not null) return tags;
        
        Plugin.Logger.LogError($"Unable to decode the texture swapper user settings to get global whitelist tags.");

        return [];

    }
    
    public static IList<string> getBlackListTags(bool authorizedUser, List<string> extraBlackList) {
        var configBlackListTags = new List<string>(Plugin.config.blacklistedTags());
        

        if (!authorizedUser || Plugin.config.enableGlobalBlacklist()) {
            configBlackListTags.AddRange(extraBlackList);
        }
        
        configBlackListTags.RemoveAll(Plugin.config.whitelistedTags().Contains);

        configBlackListTags.AddRange(getUserGlobalBlacklist());
        configBlackListTags.RemoveAll(getUserGlobalWhitelist().Contains);

        return configBlackListTags;
    }
}