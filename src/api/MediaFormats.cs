using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using HarmonyLib;
using ImageMagick;
using io.wispforest.textureswapper.utils;
using io.wispforest.util;
using NAudio.Wave;
using Sirenix.Utilities;
using UnityEngine;

namespace io.wispforest.textureswapper.api;

public static class MediaFormats {
    private static readonly Dictionary<string, MediaFormat> VALID_MEDIA_TYPES = new ();

    private static readonly Dictionary<string, MagickFormat> ENUM_MAP = new (); 
    
    public static MediaType getType(string uri) {
        var extension = Path.GetExtension(uri).Replace(".", "");

        return getFormat(extension).getType();
    }
    
    public static MediaFormat getAFormat(params string[] names) {
        foreach (var name in names) {
            var type = getFormat(name);

            if (type is not UnimplementedMediaFormat) return type;
        }

        return new UnimplementedMediaFormat(names);
    }

    public static MediaFormat getFormatFromUrl(string url) {
        return getFormat(HttpClientUtils.getFormatString(url));
    }

    public static MediaFormat getFormat(string name) {
        if (ENUM_MAP.Count <= 0) {
            var type = typeof(MagickFormat);

            var formats = Enum.GetValues(type);
        
            foreach (var obj in formats) {
                if (obj is not null && obj is MagickFormat format) {
                    ENUM_MAP[Enum.GetName(type, format)!.ToLower()] = format;
                }
            }
        }
        
        return ENUM_MAP.TryGetValue(name, out var value) ? getFormat(value) : new UnimplementedMediaFormat(name);
    }

    public static MediaFormat getFormat(MagickFormat format) {
        foreach (var mediaType in VALID_MEDIA_TYPES.Values) {
            if (mediaType.getFormats().Contains(format)) {
                return mediaType;
            }
        }

        return new UnimplementedMediaFormat(format);
    }
    
    public static void registerFormat(MediaFormat format) {
        if (!VALID_MEDIA_TYPES.ContainsKey(format.name())) {
            VALID_MEDIA_TYPES[format.name()] = format;
        }
    }

    public static List<string> getValidMediaPatterns() {
        return VALID_MEDIA_TYPES.Values
                .SelectMany(type => type.filePatterns())
                .ToList();
    }
    
    static MediaFormats(){
        registerFormat(PNG.INSTANCE);
        registerFormat(JPEG.INSTANCE);
        registerFormat(GIF.INSTANCE);
        registerFormat(WEBP.INSTANCE);
        registerFormat(WEBM.INSTANCE);
        registerFormat(MP4.INSTANCE);
        registerFormat(MP3.INSTANCE);
    }
}

public class MP3 : MediaFormat {

    public static readonly MP3 INSTANCE = new MP3();
    
    public override string name() => "mp3";
    public override string primaryExtension() => "mp3";
    public override List<string> additionalExtensions() => [];
    
    public override MediaType getType() => MediaType.AUDIO;

    public override ISet<MagickFormat> getFormats() => new ReadOnlySet<MagickFormat>();

    public override SwapperImpl? decodeData(Identifier id, RawMediaData data) {
        try {
            using MemoryStream mp3Stream = new MemoryStream(data.getBytes());
            using Mp3FileReader mp3FileReader = new Mp3FileReader(mp3Stream);
            using WaveStream waveStream = WaveFormatConversionStream.CreatePcmStream(mp3FileReader);
            
            byte[] pcmBytes = new byte[waveStream.Length];
            waveStream.Read(pcmBytes, 0, (int)waveStream.Length);

            float[] pcmFloat = new float[pcmBytes.Length / 4]; // 4 bytes per float
            Buffer.BlockCopy(pcmBytes, 0, pcmFloat, 0, pcmBytes.Length);

            AudioClip audioClip = AudioClip.Create(
                    data.id.Path,
                    pcmFloat.Length / waveStream.WaveFormat.Channels,
                    waveStream.WaveFormat.Channels,
                    waveStream.WaveFormat.SampleRate,
                    false
            );

            audioClip.SetData(pcmFloat, 0);
            return new AudioSwapper(id, audioClip);
        } catch (Exception ex) {
            Plugin.logIfDebugging(source => source.LogError($"MP3 threw a magic exception [{data.url}]: {ex.Message}"));
            Debug.LogError($"Error creating AudioClip from MP3 bytes: {ex.Message}");
            return null;
        }
    }
}

public class PNG : MediaFormat {
    public static readonly PNG INSTANCE = new PNG();
    
    public override string name() => "png";
    
    public override string primaryExtension() => "png";
    public override List<string> additionalExtensions() => [];
    
    public override MediaType getType() => MediaType.IMAGE;
    
    public override ISet<MagickFormat> getFormats() => new ReadOnlySet<MagickFormat>(
            MagickFormat.Png, MagickFormat.Png00, MagickFormat.Png8, MagickFormat.Png24,  MagickFormat.Png32, MagickFormat.Png48, MagickFormat.Png64);

    public override SwapperImpl? decodeData(Identifier id, RawMediaData data) {
        data.attemptToCacheFile();
        
        var material = MaterialUtils.loadMaterialFromBytes(data);
        
        return material is not null ? new MaterialSwapper(id, material) : null;
    }
}

public class WEBP : MediaFormat {
    public static readonly WEBP INSTANCE = new WEBP();
    
    public override string name() => "webp";
    
    public override string primaryExtension() => "webp";
    public override List<string> additionalExtensions() => [];
    
    public override MediaType getType() => MediaType.IMAGE;
    
    public override ISet<MagickFormat> getFormats() => new ReadOnlySet<MagickFormat>(MagickFormat.WebP);

    public override SwapperImpl? decodeData(Identifier id, RawMediaData data) {
        MultiThreadHelper.run(ENCODING_GROUP, () => {
            var hasCachedFile = data.attemptToCacheFile(bytes => {
                try {
                    using var image = new MagickImage(bytes);
                    // Set the output format to PNG
                    image.Format = MagickFormat.Png;

                    using var memoryStream = new MemoryStream();
                    image.Write(memoryStream);

                    return memoryStream.ToArray();
                }
                catch (MagickException ex) {
                    Plugin.logIfDebugging(source => source.LogError($"WEBP threw a magic exception [{data.url}]: {ex.Message}"));

                    return null;
                }
            });

            SwapperImpl? handler = null;
            
            if (hasCachedFile) {
                var material = MaterialUtils.loadMaterialFromBytes(data.getBytes(), id);
                
                handler = material is not null ? new MaterialSwapper(id, material) : null;
            }

            MediaSwapperStorage.storeHandler(id, data.mediaInfo, handler);
        });

        return new DelayedMeshSwapper(id, true);
    }
}

public class MP4 : MediaFormat {
    public static readonly MP4 INSTANCE = new ();

    public override string name() => "mp4";
    
    public override string primaryExtension() => "mp4";
    public override List<string> additionalExtensions() => [];

    public override ISet<MagickFormat> getFormats() => new ReadOnlySet<MagickFormat>(MagickFormat.Mp4);

    public override MediaType getType() => MediaType.VIDEO;
    
    public override SwapperImpl? decodeData(Identifier id, RawMediaData data) {
        data.attemptToCacheFile();
        
        return new VideoSwapper(id, data.getCacheFilePath(), data.mediaInfo, !data.mediaInfo.hasAudio);
    }
}

public class WEBM : MediaFormat {
    public static readonly HashSet<string> validVideoCodecs = ["vp8"];
    public static readonly HashSet<string> validAudioCodecs = ["vorpis"];
    
    public static readonly WEBM INSTANCE = new ();

    public override string name() => "webm";
    
    public override string primaryExtension() => "webm";
    public override List<string> additionalExtensions() => [];

    public override MediaType getType() => MediaType.VIDEO;
    
    public override ISet<MagickFormat> getFormats() => new ReadOnlySet<MagickFormat>(MagickFormat.WebM);

    public override SwapperImpl? decodeData(Identifier id, RawMediaData data) {
        MultiThreadHelper.run(ENCODING_GROUP, () => {
            MediaSwapperStorage.storeHandler(id, data.mediaInfo, createHandler(id, data));
        });
        
        return new DelayedMeshSwapper(id, !data.mediaInfo.hasAudio);
    }

    public static SwapperImpl? createHandler(Identifier id, RawMediaData data) {
        var hasCachedFile = data.attemptToCacheFile(bytes => {
            var videoCodecEmpty = data.mediaInfo.videoCodec.IsNullOrWhitespace();
            var audioCodecEmpty = data.mediaInfo.audioCodec.IsNullOrWhitespace();
        
            var isEmptyVideo = videoCodecEmpty && audioCodecEmpty;
            
            if (isEmptyVideo) return null;
            
            var invalidVideoCodec = !videoCodecEmpty && !validVideoCodecs.Contains(data.mediaInfo.videoCodec);
            var invalidAudioCodec = !audioCodecEmpty && !validAudioCodecs.Contains(data.mediaInfo.audioCodec);
            
            if (!Plugin.ConfigAccess.allowTranscodingVideos()) {
                if (invalidVideoCodec || invalidAudioCodec) return null;
            
                return bytes;
            }
            
            try {
                if (invalidVideoCodec || invalidAudioCodec) {
                    var filePath = Path.Combine(Plugin.TempVideoStoragePath, id.Namespace, id.Path);
                    
                    Plugin.logIfDebugging(source => source.LogInfo($"A WEBM is being converted as its invalid: [ValidVideoCodec: {!invalidVideoCodec}, ValidAudioCodec: {!invalidAudioCodec}]"));
                    
                    var outputBytes = new MemoryStream(bytes);
                    var bl = FFMpegArguments.FromFileInput(
                                    new FileInfo(filePath + "_input.webm"),
                                    options => options.ForceFormat("webm")
                                            .WithHardwareAcceleration(HardwareAccelerationDevice.CUDA)
                            )
                            .OutputToFile(filePath + "_output.webm",
                                    overwrite: true,
                                    options => options.ForceFormat("webm")
                                            .WithHardwareAcceleration(HardwareAccelerationDevice.CUDA)
                                            .WithConstantRateFactor(15)
                                            .ForcePixelFormat("yuv420p")
                                            .WithVideoBitrate(2000)
                                            .WithVideoCodec("libvpx") // libvpx-
                                            .WithAudioCodec("libvorbis") // libvorbis
                            )
                            .ProcessAsynchronously();

                    bl.Wait();
            
                    if (bl.Result) return outputBytes.ToArray();
                }
            } catch (Exception e) {
                Plugin.logIfDebugging(source => source.LogError($"Unable to encode the given video data to a compatible format: {e}"));
            }
            
            return bytes;
        });
        
        SwapperImpl? handler = null;
        
        if (hasCachedFile) { 
            handler = new VideoSwapper(id, data.getCacheFilePath(), data.mediaInfo, !data.mediaInfo.hasAudio);
        }

        return handler;
    }
}

public class JPEG : MediaFormat {
    public static readonly JPEG INSTANCE = new JPEG();
    
    public override string name() => "jpg";
    
    public override string primaryExtension() => "jpg";
    public override List<string> additionalExtensions() => ["jpeg", "jpe"];
    
    public override MediaType getType() => MediaType.IMAGE;
    
    public override ISet<MagickFormat> getFormats() => new ReadOnlySet<MagickFormat>(MagickFormat.Jpeg, MagickFormat.Jpg, MagickFormat.Jpe);

    public override SwapperImpl? decodeData(Identifier id, RawMediaData data) {
        data.attemptToCacheFile();
        
        var material = MaterialUtils.loadMaterialFromBytes(data);
        
        return material is not null ? new MaterialSwapper(id, material) : null;
    }
}

public class GIF : MediaFormat {
    public static readonly GIF INSTANCE = new GIF();
    
    public override string name() => "gif";
    
    public override string primaryExtension() => "gif";
    public override List<string> additionalExtensions() => [];

    public override MediaType getType() => MediaType.VIDEO;
    
    public override ISet<MagickFormat> getFormats() => new ReadOnlySet<MagickFormat>(MagickFormat.Gif);

    public override SwapperImpl? decodeData(Identifier id, RawMediaData data) {
        MultiThreadHelper.run(ENCODING_GROUP, () => {
            data.attemptToCacheFile(bytes => ImageUtils.convertToWebm(id, bytes));
            MainThreadHelper.runOnMainThread(() => {
                MediaSwapperStorage.storeHandler(id, data.mediaInfo, WEBM.createHandler(id, data));
            });
        });
        
        // Task.Run(() => {
        //     if (!Plugin.ConfigAccess.onlyFirstAnimationFrame()) {
        //         try {
        //             // var imageData = ImageUtils.getInitialFrameOfAnimatedImage(data.getBytes());
        //             //
        //             // MainThreadHelper.runOnMainThread(() => {
        //             //     var initialMaterial = MaterialUtils.loadMaterialFromBytes(imageData, id, postFix: $"_{0}");
        //             //     var filePath = data.getCacheFilePath();
        //             //
        //             //     var handler = new MaterialListHandler(id, initialMaterial, () => {
        //             //         return ImageUtils.convertAnimatedImageToSlices(File.ReadAllBytes(filePath), id: id);
        //             //     });
        //             //
        //             //     MediaSwapperStorage.storeHandler(id, data, handler);
        //             // });
        //         } catch (Exception ex) {
        //             Plugin.logIfDebugging(source => source.LogError($"Gif threw an exception [{data.url}]: {ex.Message}"));
        //         }
        //     } else {
        //         try {
        //             
        //             var imageData = ImageUtils.getInitialFrameOfAnimatedImage(data.getBytes());
        //         
        //             MainThreadHelper.runOnMainThread(() => {
        //                 var material = MaterialUtils.loadMaterialFromBytes(imageData, id);
        //                 var handler = material is not null ? new MaterialHandler(id, material) : null;
        //                 
        //                 MediaSwapperStorage.storeHandler(id, data, handler);
        //             });
        //         } catch (Exception ex) {
        //             Plugin.logIfDebugging(source => source.LogError($"Gif threw an exception [{data.url}]: {ex.Message}"));
        //         }
        //     }
        // });
        
        return new DelayedMeshSwapper(id, true);
    }
}

public class UnimplementedMediaFormat : MediaFormat {
    private readonly string _extension;
    private readonly IList<string> _extensions;
    private readonly ISet<MagickFormat> _formats;

    public UnimplementedMediaFormat(string extension, MagickFormat format) {
        _extension = extension;
        _formats = new ReadOnlySet<MagickFormat>(format);
    }
    
    public UnimplementedMediaFormat(string extension) {
        _extension = extension;
        _formats = new ReadOnlySet<MagickFormat>();
    }
    
    public UnimplementedMediaFormat(params string[] extensions) {
        _extension = extensions[0];
        _extensions = extensions.getSublistSafe(1);
    }
    
    public UnimplementedMediaFormat(MagickFormat format) {
        _extension = Enum.GetName(typeof(MagickFormat), format)?.ToLower() ?? "unknown";
        _formats = new ReadOnlySet<MagickFormat>(format);
    }

    public override string name() => _extension;

    public override string primaryExtension() => _extension;
    public override List<string> additionalExtensions() => [];

    public override ISet<MagickFormat> getFormats() => _formats;

    public override MediaType getType() => MediaType.UNKNOWN;

    public override SwapperImpl? decodeData(Identifier id, RawMediaData data) {
        return new EmptySwapper(id);
    }
}

public abstract class MediaFormat {
    
    public static readonly SemaphoreIdentifier ENCODING_GROUP = new SemaphoreIdentifier(
            Identifier.of("texture_swapper", "encoding"), 
            maxCount: 4
    );
    
    public abstract string name();
    
    public abstract string primaryExtension();
    
    public abstract List<string> additionalExtensions();

    public virtual IEnumerable<string> filePatterns() {
        return new List<string>([primaryExtension()])
                .Concat(additionalExtensions())
                .Select(s => $"*.{s}");
    }
    
    public abstract ISet<MagickFormat> getFormats();

    public abstract MediaType getType();

    public abstract SwapperImpl? decodeData(Identifier id, RawMediaData data);

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is MediaFormat typeObj && name().Equals(typeObj.name());
    }

    public override int GetHashCode() {
        return this.name().GetHashCode();
    }
}

public enum MediaType {
    IMAGE,
    VIDEO,
    AUDIO,
    UNKNOWN
}