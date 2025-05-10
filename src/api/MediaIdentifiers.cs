using System.Collections.Generic;
using System.IO;
using System.Linq;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper.api;

public class MediaIdentifiers {
    public static readonly Identifier MISSING = Identifier.of("local", "swapper_missing_image");
    public static readonly Identifier ERROR = Identifier.of("local", "swapper_error_image");
    public static readonly Identifier LOADING = Identifier.of("local", "swapper_loading_image");
    public static readonly Identifier CENSORED = Identifier.of("local", "swapper_censored_image");

    public static List<Identifier> DEFAULT_DATA_VARIANTS => [MISSING, ERROR, LOADING, CENSORED];
    
    internal static void initErrorImages(string pluginFolder) {
        IList<string> images = ["swapper_missing_image.png", "swapper_error_image.png", "swapper_loading_image.png", "swapper_censored_image.png"];

        var query = LocalMediaQuery.ofFiles(images.Select(s => Path.Combine(pluginFolder, s)));

        query.syncedTask = true;
        
        LocalMediaQueryType.INSTANCE.executeQuery(query);
    }
}