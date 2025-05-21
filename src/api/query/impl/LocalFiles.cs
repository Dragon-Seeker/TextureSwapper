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
            Endecs.STRING.listOf().optionalFieldOf<LocalMediaQuery>("tags", s => s.tags, () => []),
            (directory, files, rating, tags) => new LocalMediaQuery(directory, files, rating, tags)
    );

    public static Endec<LocalMediaQuery> Endec() => ENDEC;
    
    private string? directory;
    private IList<string> files;
    public MediaRating rating { get; }
    public IList<string> tags { get; }

    public bool syncedTask {get; set;}

    private LocalMediaQuery(string? directory, IList<string> files, MediaRating rating, IList<string> tags) {
        this.directory = directory;
        this.files = files;
        this.rating = rating;
        this.tags = tags;
    }

    public static LocalMediaQuery ofDirectory(string directory, MediaRating rating = MediaRating.SAFE, IList<string>? tags = null) {
        return new LocalMediaQuery(directory, new List<string>(), rating, []);
    }
    
    public static LocalMediaQuery ofFile(string file, MediaRating rating = MediaRating.SAFE, IList<string>? tags = null) {
        return new LocalMediaQuery(null, [file], rating, []);
    }
    
    public static LocalMediaQuery ofFiles(IEnumerable<string> files, MediaRating rating = MediaRating.SAFE, IList<string>? tags = null) {
        return new LocalMediaQuery(null, new List<string>(files), rating, []);
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
                    loadTextureFromBytes(file, files.Item1, File.ReadAllBytes(file), data.rating, data.tags);
                } else {
                    MultiThreadHelper.run(createSemaphoreIdentifier(), () => File.ReadAllBytesAsync(file).ContinueWith(task => {
                        if (task.IsCompleted) {
                            loadTextureFromBytes(file, files.Item1, task.Result, data.rating, data.tags);
                        }
                    }));
                }
                
            } catch (Exception e) {
                Plugin.logIfDebugging(source => source.LogError($"Was unable to process the given file in the local query {file}: {e}"));
            }
        }
    }
    
    private static void loadTextureFromBytes(string file, string origin, byte[]? bytes, MediaRating rating, IList<string> tags) {
        try {
            if (bytes is null) return;
            
            var rawMedia = new RawMediaData(file, bytes, new LocalMediaQueryResult(origin, rating, tags), unknownHostType: "local");

            MediaSwapperStorage.storeRawMediaData(rawMedia);
        } catch(Exception e) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to read local image from {file}: {e.Message}"));
        }
    }
}

public class LocalMediaQueryResult : MediaQueryResult, EndecGetter<LocalMediaQueryResult>, RatedMediaResult, TaggedMediaResult {
    public static readonly StructEndec<LocalMediaQueryResult> ENDEC = StructEndecBuilder.of(
            Endecs.STRING.fieldOf<LocalMediaQueryResult>("origin", s => s.origin),
            MediaRatingUtils.ENDEC.fieldOf<LocalMediaQueryResult>("rating", s => s.rating),
            Endecs.STRING.listOf().fieldOf<LocalMediaQueryResult>("tags", s => s.tags),
            (info, rating, tags) => new LocalMediaQueryResult(info, rating, tags)
    );
    
    public string origin { get; }
    public MediaRating rating { get; }
    public IList<string> tags { get; }

    public LocalMediaQueryResult(string origin, MediaRating rating, IList<string> tags) {
        this.origin = origin;
        this.rating = rating;
        this.tags = tags;
    }

    public static Endec<LocalMediaQueryResult> Endec() => ENDEC;

    public override Identifier getQueryTypeId() => LocalMediaQueryType.ID;

    public MediaRating getRating() => rating;

    public bool hasTag(string tag) => tags.Contains(tag);
}