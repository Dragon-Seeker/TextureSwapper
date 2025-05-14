using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BepInEx.Logging;
using FFMpegCore;
using ImageMagick;
using io.wispforest.impl;
using io.wispforest.textureswapper.utils;
using NAudio.Wave;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.api;

public record MediaInfo : EndecGetter<MediaInfo> {

    public static readonly MediaInfo EMPTY = ofError("");
    
    public static readonly Endec<MediaInfo> ENDEC = StructEndecBuilder.of(
            Endecs.INT.fieldOf<MediaInfo>("width", s => s.width),
            Endecs.INT.fieldOf<MediaInfo>("height", s => s.height), 
            MediaFormats.FORMAT_ENDEC.optionalFieldOf<MediaInfo>("ext", s => s.format, new UnimplementedMediaFormat("unknown")),
            Endecs.INT.optionalFieldOf<MediaInfo>("size", s => s.size, () => 0), 
            Endecs.STRING.optionalFieldOf<MediaInfo>("md5", s => s.md5Hash, () => ""),
            Endecs.STRING.fieldOf<MediaInfo>("url", s => s.uri),
            Endecs.DOUBLE.optionalFieldOf<MediaInfo>("duration", s => s.duration, () => -1),
            Endecs.STRING.optionalFieldOf<MediaInfo>("video_codec", s => s.videoCodec, () => ""),
            Endecs.STRING.optionalFieldOf<MediaInfo>("audio_codec", s => s.audioCodec, () => ""),
            Endecs.INT.optionalFieldOf<MediaInfo>("channels", s => s.channels, () => -1),
            Endecs.INT.optionalFieldOf<MediaInfo>("frequency", s => s.frequency, () => -1),
            Endecs.BOOLEAN.optionalFieldOf<MediaInfo>("is_error", s => s.isError, () => false),
            (width, height, format, size, md5Hash, uri, duration, videoCodec, audioCodec, channels, frequency, isError) => {
                return new MediaInfo(width, height, format, size, md5Hash, uri, duration, videoCodec, audioCodec, channels, frequency, isError);
            });

    public static Endec<MediaInfo> Endec() => ENDEC;

    public MediaFormat format { get; }
    public int size { get; }
    public string md5Hash { get; }
    public string uri { get; private set; }
    
    public int width { get; }
    public int height { get; }
    
    public double duration { get; }

    public bool hasVideo => !videoCodec.IsNullOrWhitespace();
    
    public string videoCodec { get; }

    public bool hasAudio => !audioCodec.IsNullOrWhitespace();
    
    public string audioCodec { get; }
    public int channels  { get; }
    public int frequency  { get; }
    
    public bool isError { get; }
    
    public MediaInfo(int width, int height, MediaFormat format, int size, string md5Hash, string uri, double duration = -1, string videoCodec = "", string audioCodec = "", int channels = -1, int frequency = -1, bool isError = false) {
        this.width = width;
        this.height = height;
        this.format = format;
        this.size = size;
        this.md5Hash = md5Hash;
        this.uri = uri;
        this.isError = isError;

        this.duration = duration;

        this.videoCodec = videoCodec;
        
        this.audioCodec = audioCodec;
        this.channels = channels;
        this.frequency = frequency;
    }

    internal MediaInfo adjustUri(Func<string, string> conversion) {
        this.uri = conversion(this.uri);

        return this;
    }

    public int lengthSamples() {
        return (int) (long) duration * frequency;
    }

    public virtual bool Equals(MediaInfo? other) {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return format.Equals(other.format) 
               && size == other.size 
               && md5Hash == other.md5Hash 
               && uri == other.uri 
               && width == other.width 
               && height == other.height 
               && Math.Abs(duration - other.duration) < 0.01 
               && videoCodec == other.videoCodec 
               && audioCodec == other.audioCodec 
               && channels == other.channels 
               && frequency == other.frequency 
               && isError == other.isError;
    }

    public override int GetHashCode() {
        var hashCode = new HashCode();
        hashCode.Add(format);
        hashCode.Add(size);
        hashCode.Add(md5Hash);
        hashCode.Add(uri);
        hashCode.Add(width);
        hashCode.Add(height);
        hashCode.Add(duration);
        hashCode.Add(videoCodec);
        hashCode.Add(audioCodec);
        hashCode.Add(channels);
        hashCode.Add(frequency);
        hashCode.Add(isError);
        return hashCode.ToHashCode();
    }

    public override string ToString() {
        return
                $"{nameof(format)}: {format}, {nameof(size)}: {size}, {nameof(md5Hash)}: {md5Hash}, {nameof(uri)}: {uri}, {nameof(width)}: {width}, {nameof(height)}: {height}, {nameof(duration)}: {duration}, {nameof(hasVideo)}: {hasVideo}, {nameof(videoCodec)}: {videoCodec}, {nameof(hasAudio)}: {hasAudio}, {nameof(audioCodec)}: {audioCodec}, {nameof(channels)}: {channels}, {nameof(frequency)}: {frequency}, {nameof(isError)}: {isError}";
    }

    public static MediaInfo ofError(string uri) {
        return new(0, 0, new UnimplementedMediaFormat(""), 0, "", uri, isError: true);
    }

    public static MediaInfo of(string uri, byte[] data, MediaInfo? extraInfo) {
        Queue<Exception> exceptions = new Queue<Exception>();

        MediaInfo? info = null;

        if (data.Length > 0) {
            info = getInfoAsImage(uri, data, extraInfo).getLeftOrMapRight(ex => {
                exceptions.Enqueue(ex);
                return null;
            }) ?? getInfoAsVideo(uri, data, extraInfo).getLeftOrMapRight(ex => {
                exceptions.Enqueue(ex);
                return null;
            }) ?? getInfoAsAudio(uri, data, extraInfo).getLeftOrMapRight(ex => {
                exceptions.Enqueue(ex);
                return null;
            });
        }

        if (info is not null) {
            Plugin.logIfDebugging(source => {
                source.LogWarning($"Was able to parse the given data to find the MediaInfo: {info.ToString()}");
            });
            return info;
        }
        
        Plugin.logIfDebugging(source => {
            if (exceptions.Count > 0) {
                source.LogError($"Media Info parse threw an exceptions [{uri}]: ");
                while (exceptions.TryDequeue(out Exception ex)) {
                    source.LogError($"  {ex}\n");
                }
            } else {
                source.LogError($"Was unable to parse any info for the given media: {uri}");
            }
        });
            
        return ofError(uri);
    }
    
    private static (MediaInfo? info, Exception? ex) getInfoAsImage(string uri, byte[] data, MediaInfo? extraInfo) {
        try {
            using var frameHelper = new MagickImage(data);
                
            return new (new MediaInfo((int)frameHelper.Width, (int)frameHelper.Height, MediaFormats.getFormat(frameHelper.Format), extraInfo?.size ?? data.Length, extraInfo?.md5Hash ?? "", uri), null);
        } catch (Exception ex) {
            return new (null, ex);
        }
    }
    
    private static (MediaInfo? info, Exception? ex) getInfoAsAudio(string uri, byte[] data, MediaInfo? extraInfo) {
        try {
            using var audioFileReader = new Mp3FileReader(new MemoryStream(data, writable: false));

            MediaFormat? type = MP3.INSTANCE;
            
            return (new MediaInfo(-1, -1, type, extraInfo?.size ?? data.Length, extraInfo?.md5Hash ?? "", uri, audioFileReader.TotalTime.TotalSeconds, "", "MpegLayer3", audioFileReader.WaveFormat.Channels, audioFileReader.WaveFormat.SampleRate), null);
        }
        catch (Exception ex) {
            return new (null, ex);
        }
    }
    
    private static (MediaInfo? info, Exception? ex) getInfoAsVideo(string uri, byte[] data, MediaInfo? extraInfo) {
        try {
            var mediaInfo = FFProbe.Analyse(new MemoryStream(data, false));
            
            var videoInfo = mediaInfo.PrimaryVideoStream;
            var audioInfo = mediaInfo.PrimaryAudioStream;
            
            var hasVideo = videoInfo is not null;
            var hasAudio = audioInfo is not null;
            
            if (!hasVideo && !hasAudio) {
                return (null, new ArgumentException("Unable to get any audio or video info from the given file"));
            }
            
            var audioCodec = hasAudio ? audioInfo.CodecName : "";
            
            var channels = hasAudio ? audioInfo.Channels : -1;
            var frequency = hasAudio ? audioInfo.SampleRateHz : -1;
            
            var type = MediaFormats.getAFormat(mediaInfo.Format.FormatName.Split(","));
            
            Plugin.logIfDebugging(source => source.LogError($"Type: {type}, Name: {mediaInfo.Format.FormatName}"));
        
            var videoCodec = hasVideo ? videoInfo.CodecName : "";
            
            var width = hasVideo ? videoInfo.Width : -1;
            var height = hasVideo ? videoInfo.Height : -1;
            
            return (new MediaInfo(width, height, type, extraInfo?.size ?? data.Length, extraInfo?.md5Hash ?? "", uri, mediaInfo.Duration.TotalSeconds, videoCodec, audioCodec, channels, frequency), null);
        } catch (Exception ex) {
            return new (null, ex);
        }
    }
}

