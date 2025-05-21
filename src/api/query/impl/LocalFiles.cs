using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImageMagick;
using io.wispforest.impl;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.utils;
using UnityEngine;

namespace io.wispforest.textureswapper.api.query.impl;

public class LocalFiles { }

public class LocalMediaQuery : MediaQuery, EndecGetter<LocalMediaQuery> {

    public static readonly StructEndec<LocalMediaQuery> ENDEC = StructEndecBuilder.of(
            Endecs.STRING.optionalFieldOf<LocalMediaQuery>("directory", query => query.directory, () => null),
            Endecs.STRING.listOf().optionalFieldOf<LocalMediaQuery>("files", query => query.files, () => []),
            MediaRatingUtils.ENDEC.fieldOf<LocalMediaQuery>("rating", s => s.rating),
            (directory, files, rating) => new LocalMediaQuery(directory, files, rating)
    );

    public static Endec<LocalMediaQuery> Endec() => ENDEC;
    
    private string? directory;
    private IList<string> files;
    public MediaRating rating { get; }

    public bool syncedTask {get; set;}

    private LocalMediaQuery(string? directory, IList<string> files, MediaRating rating) {
        this.directory = directory;
        this.files = files;
        this.rating = rating;
    }

    public static LocalMediaQuery ofDirectory(string directory, MediaRating rating = MediaRating.SAFE) {
        return new LocalMediaQuery(directory, new List<string>(), rating);
    }
    
    public static LocalMediaQuery ofFile(string file, MediaRating rating = MediaRating.SAFE) {
        return new LocalMediaQuery(null, [file], rating);
    }
    
    public static LocalMediaQuery ofFiles(IEnumerable<string> files, MediaRating rating = MediaRating.SAFE) {
        return new LocalMediaQuery(null, new List<string>(files), rating);
    }

    public override Identifier getQueryTypeId() {
        return LocalMediaQueryType.ID;
    }

    public (string, IList<string>) gatherFiles() {
        if (directory != null && Directory.Exists(directory)) {
            return (
                    directory, 
                    MediaFormats.getValidMediaPatterns()
                    .SelectMany(pattern => Directory.GetFiles(directory, pattern))
                    .ToList()
            );
        }
        
        return ("files", files);

    }
}

public class LocalMediaQueryType : MediaQueryType<LocalMediaQuery, LocalMediaQueryResult> {
    public static readonly Identifier ID = Identifier.of("texture_swapper", "local");

    public static readonly LocalMediaQueryType INSTANCE = new ();

    public override StructEndec<LocalMediaQueryResult> getResultEndec() => LocalMediaQueryResult.ENDEC;
    public override StructEndec<LocalMediaQuery> getDataEndec() => LocalMediaQuery.ENDEC;
    public override Identifier getLookupId() => ID;

    public override void executeQuery(LocalMediaQuery data) {
        var files = data.gatherFiles();
        
        foreach (var file in files.Item2) {
            if (Regex.IsMatch(file, @"(\.\.(\\|\/|$))")) {
                Plugin.logIfDebugging(source => source.LogError($"Unable to handle the given Local file [{file}] as it matches against the pattern [{@"(\.\.(\\|\/|$))"}] possibly indicating Path Traversal!"));
                
                continue;
            }
            
            try {
                var parentDir = FileUtils.getParentDirectory(file);

                if (Plugin.RAW_NAMES.Contains(parentDir)) {
                    parentDir = FileUtils.getParentDirectory(parentDir);
                }
                
                MediaSwapperStorage.addIdAndTryToSetupType(file, unknownHostType: parentDir ?? "local");
                
                if (data.syncedTask) {
                    loadTextureFromBytes(file, files.Item1, File.ReadAllBytes(file), data.rating);
                } else {
                    MultiThreadHelper.run(createSemaphoreIdentifier(), () => File.ReadAllBytesAsync(file).ContinueWith(task => {
                        if (task.IsCompleted) {
                            loadTextureFromBytes(file, files.Item1, task.Result, data.rating);
                        }
                    }));
                }
                
            } catch (Exception e) {
                Plugin.logIfDebugging(source => source.LogError($"Was unable to process the given file in the local query {file}: {e}"));
            }
        }
    }
    
    private static void loadTextureFromBytes(string file, string origin, byte[]? bytes, MediaRating rating) {
        try {
            if (bytes is null) return;
            
            var rawMedia = new RawMediaData(file, bytes, new LocalMediaQueryResult(origin, rating), unknownHostType: "local");

            MediaSwapperStorage.storeRawMediaData(rawMedia);
        } catch(Exception e) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to read local image from {file}: {e.Message}"));
        }
    }
}

public class LocalMediaQueryResult : MediaQueryResult, EndecGetter<LocalMediaQueryResult>, RatedMediaResult {
    public static readonly StructEndec<LocalMediaQueryResult> ENDEC = StructEndecBuilder.of(
            Endecs.STRING.fieldOf<LocalMediaQueryResult>("origin", s => s.origin),
            MediaRatingUtils.ENDEC.fieldOf<LocalMediaQueryResult>("rating", s => s.rating),
            (info, rating) => new LocalMediaQueryResult(info, rating)
    );
    
    public string origin { get; }
    public MediaRating rating { get; }

    public LocalMediaQueryResult(string origin, MediaRating rating) {
        this.origin = origin;
        this.rating = rating;
    }

    public static Endec<LocalMediaQueryResult> Endec() => ENDEC;

    public override Identifier getQueryTypeId() => LocalMediaQueryType.ID;

    public MediaRating getRating() => rating;
}