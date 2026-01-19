using System.Collections.Generic;
using System.IO;
using System.Linq;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper.api;

public class MediaIdentifiers {
    public static Identifier MISSING { get; private set; }
    public static Identifier ERROR { get; private set; }
    public static Identifier LOADING { get; private set; }
    public static Identifier CENSORED { get; private set; }

    public static List<Identifier> DEFAULT_DATA_VARIANTS => [MISSING, ERROR, LOADING, CENSORED];
    
    internal static void initErrorImages(string pluginFolder) {
        IList<string> images = ["swapper_missing_image.png", "swapper_error_image.png", "swapper_loading_image.png", "swapper_censored_image.png"];

        var files = images.Select(s => Path.Combine(pluginFolder, s)).ToList();
        
        var query = LocalMediaQuery.ofFiles(Identifier.of(Plugin.id, "base_default_media_ids"), files);

        query.syncedTask = true;
        
        for (var i = 0; i < files.Count; i++) {
            var file = files[i];
            
            var parentDir = FileUtils.getParentDirectory(file);

            if (parentDir is not null && Plugin.RAW_NAMES.Contains(parentDir)) {
                parentDir = FileUtils.getParentDirectory(file, 2);
            }
                
            var id = Identifier.ofUri(file, parentDir ?? "local");

            if (i == 0) MISSING = id;
            else if (i == 1) ERROR = id;
            else if (i == 2) LOADING = id;
            else if (i == 3) CENSORED = id;
        }
        
        LocalMediaQueryType.INSTANCE.executeQuery(query);
        
        foreach (var id in DEFAULT_DATA_VARIANTS) {
            var handler = MediaSwapperStorage.getHandler(id);

            if (handler is null) {
                Plugin.Logger.LogError($"Unable to get the internal swapper media [{id}] which may cause errors, you have been warned!");
            }
        }
    }
}