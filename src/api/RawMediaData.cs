using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ImageMagick;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.utils;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.api;

public record RawMediaData {
    public Identifier id { get; }
    public string url { get; }
    
    public MediaQueryResult queryResult { get; }
    public MediaInfo mediaInfo { get; private set; }
    
    private byte[]? rawImageData;
    
    public RawMediaData(string url, byte[] rawImageData, MediaQueryResult queryResult, MediaInfo? mediaInfo = null) {
        this.id = Identifier.ofUri(url);
        this.url = url;
        this.mediaInfo = MediaInfo.of(url, rawImageData, mediaInfo);
        this.rawImageData = rawImageData;
        this.queryResult = queryResult;
    }

    private RawMediaData(string url, MediaQueryResult queryResult) {
        this.id = Identifier.ofUri(url);
        this.url = url;
        this.queryResult = queryResult;
        this.mediaInfo = MediaInfo.ofError(url);
    }

    public static async Task<RawMediaData> getData(HttpClient client, MediaInfo mediaInfo, MediaQueryResult queryResult) {
        return await getData(client, queryResult, mediaInfo.uri, mediaInfo);
    }

    public static async Task<RawMediaData> getData(HttpClient client, MediaQueryResult queryResult, string imageUrl, MediaInfo? mediaInfo = null) {
        var timeOutWindow = 120;

        if (!imageUrl.IsNullOrWhitespace()) {
            try {
                bool loadedFromCache = true;
                var mediaBytes = await tryAndGetCachedFile(imageUrl);

                if (mediaBytes is null) {
                    Plugin.logIfDebugging(() => $"Unable to get cache file for the given url: {imageUrl}");
                    loadedFromCache = false;
                    
                    var dataGrabTask = client.GetAsync(imageUrl);
                    var delayTask = Task.Delay(timeOutWindow * 1000);

                    var completedTask = await Task.WhenAny(dataGrabTask, delayTask);
                    
                    var response = completedTask == dataGrabTask && dataGrabTask.IsCompletedSuccessfully 
                            ? dataGrabTask.Result 
                            : null;
                    
                    if (response is not null) {
                        mediaBytes = await response.Content.ReadAsByteArrayAsync();
                        
                        //MediaInfo.PrintByteArray(mediaBytes);
                    }
                }

                if (mediaBytes is not null) {
                    Plugin.logIfDebugging(source => source.LogInfo($"Was able to {(loadedFromCache ? "get cached bytes for" : "download bytes from")} the given URL: {imageUrl}"));
                    
                    return new RawMediaData(imageUrl, mediaBytes, queryResult, mediaInfo);
                }
                
                // TODO: CLIENT MAY HAVE BEEN DISPOSED INSTEAD BUT NO GOOD WAY TO CHECK
                Plugin.Logger.LogError($"Unable to get image from url within {timeOutWindow} second window: {imageUrl}");
            } catch (Exception e) {
                Plugin.Logger.LogError($"An exception has occured when handling this url [{imageUrl}]: {e}");
            }
        } else {
            Plugin.Logger.LogError($"Unable to get image from url as such is not valid: {imageUrl}");
        }

        return new RawMediaData(imageUrl, queryResult);
    }

    public byte[] getBytes() => rawImageData ?? [];

    public bool isError() => mediaInfo.isError;

    private static async Task<byte[]?> tryAndGetCachedFile(string url) {
        var id = Identifier.ofUri(url);
        var urlFormat = MediaFormats.getFormatFromUrl(url);

        var lookup = (urlFormat is UnimplementedMediaFormat format)
                ? createFileLookup(id, MediaType.UNKNOWN, format.primaryExtension())
                : createFileLookup(id, urlFormat.getType(), urlFormat.primaryExtension());
        
        return await FileUtils.loadDataFromFile(lookup);
    }
    
    public async Task<byte[]?> getCachedFile() {
        var format = mediaInfo.format;

        var lookup  = createFileLookup(id, format.getType(), format.primaryExtension());

        return await FileUtils.loadDataFromFile(lookup);
    }

    public string getCacheFilePath() {
        var format = mediaInfo.format;
        
        var lookup = createFileLookup(id, format.getType(), format.primaryExtension());
        
        return lookup.getFilePath();
    }
    
    public bool attemptToCacheFile(Func<byte[], byte[]?>? conversion = null) {
        if (isError() || rawImageData is null || SwapperComponentSetupUtils.isCensored(queryResult) || queryResult is LocalMediaQueryResult) {
            return false;
        }

        var format = mediaInfo.format;
        
        var lookup = createFileLookup(id, format.getType(), format.primaryExtension());
        var file = lookup.getFilePath();
        
        if (File.Exists(file)) return true;

        if (conversion is not null) {
            var convertedData = conversion(rawImageData);

            if (convertedData == null) return false;
            
            rawImageData = convertedData;

            mediaInfo = MediaInfo.of(url, rawImageData, mediaInfo);
            
            attemptToCacheFile(null);
        } else {
            FileUtils.createFileFromBytes(rawImageData, file);
        }

        return true;
    }

    private static FileLookupHelper createFileLookup(Identifier id, MediaType type, string extension = "*") {
        var basePath = type switch {
                MediaType.VIDEO => Plugin.TempVideoStoragePath,
                MediaType.AUDIO => Plugin.TempAudioStoragePath,
                MediaType.IMAGE => Plugin.TempImageStoragePath,
                _ => Plugin.TempStoragePath
        };

        basePath = Path.Combine(basePath, id.Namespace);

        return new FileLookupHelper(basePath, id.Path, $"{id.Path}.{extension}");
    }
}

