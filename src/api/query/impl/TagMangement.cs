using System;
using System.Collections.Generic;
using System.IO;
using io.wispforest.impl;
using io.wispforest.textureswapper.endec.format.newtonsoft;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.api.query.impl;

public class TagMangement {
    
    public static IList<string> getUserGlobalBlacklist() {
        var tags = UserSettingsAccess.getDefinedSettings()?.blackListData.tags;

        if (tags is not null) return tags;
        
        Plugin.Logger.LogError($"Unable to decode the texture swapper user settings to get global blacklist tags.");

        return [];

    }
    
    public static IList<string> getUserGlobalWhitelist() {
        var tags = UserSettingsAccess.getDefinedSettings()?.whiteListData.tags;

        if (tags is not null) return tags;
        
        Plugin.Logger.LogError($"Unable to decode the texture swapper user settings to get global whitelist tags.");

        return [];

    }
    
    public static IList<string> getBlackListTags(bool authorizedUser, List<string> extraBlackList) {
        var configBlackListTags = new List<string>(Plugin.ConfigAccess.blackListTags);
        

        if (!authorizedUser || Plugin.ConfigAccess.enableGlobalBlacklist()) {
            configBlackListTags.AddRange(extraBlackList);
        }
        
        configBlackListTags.RemoveAll(Plugin.ConfigAccess.whiteListTags.Contains);

        configBlackListTags.AddRange(getUserGlobalBlacklist());
        configBlackListTags.RemoveAll(getUserGlobalWhitelist().Contains);

        return configBlackListTags;
    }
}