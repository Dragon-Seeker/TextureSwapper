using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
            (directory, files) => new LocalMediaQuery(directory, files)
    );

    public static Endec<LocalMediaQuery> Endec() => ENDEC;
    
    private string? directory;
    private IList<string> files;

    public bool syncedTask {get; set;}

    private LocalMediaQuery(string? directory, IList<string> files) {
        this.directory = directory;
        this.files = files;
    }

    public static LocalMediaQuery ofDirectory(string directory) {
        return new LocalMediaQuery(directory, new List<string>());
    }
    
    public static LocalMediaQuery ofFile(string file) {
        return new LocalMediaQuery(null, [file]);
    }
    
    public static LocalMediaQuery ofFiles(IEnumerable<string> files) {
        return new LocalMediaQuery(null, new List<string>(files));
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
            try {
                MediaSwapperStorage.addIdAndTryToSetupType(file);
                
                if (data.syncedTask) {
                    loadTextureFromBytes(file, files.Item1, File.ReadAllBytes(file));
                } else {
                    MultiThreadHelper.run(createSemaphoreIdentifier(), () => File.ReadAllBytesAsync(file).ContinueWith(task => {
                        if (task.IsCompleted) {
                            loadTextureFromBytes(file, files.Item1, task.Result);
                        }
                    }));
                }
                
            } catch (Exception e) {
                Plugin.logIfDebugging(source => source.LogError($"Was unable to process the given file in the local query {file}: {e}"));
            }
        }
    }
    
    private static void loadTextureFromBytes(string file, string origin, byte[]? bytes) {
        try {
            if (bytes is null) return;
            
            var rawMedia = new RawMediaData(file, bytes, new LocalMediaQueryResult(origin));

            MediaSwapperStorage.storeRawMediaData(rawMedia);
        } catch(Exception e) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to read local image from {file}: {e.Message}"));
        }
    }
}

public class LocalMediaQueryResult : MediaQueryResult, EndecGetter<LocalMediaQueryResult> {
    public static readonly StructEndec<LocalMediaQueryResult> ENDEC = StructEndecBuilder.of(
            Endecs.STRING.fieldOf<LocalMediaQueryResult>("origin", s => s.origin),
            info => new LocalMediaQueryResult(info)
    );
    
    public string origin { get; }

    public LocalMediaQueryResult(string origin) {
        this.origin = origin;
    }

    public static Endec<LocalMediaQueryResult> Endec() => ENDEC;

    public override Identifier getQueryTypeId() {
        return LocalMediaQueryType.ID;
    }
}