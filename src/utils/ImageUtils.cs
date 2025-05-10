using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using ImageMagick;
using ImageMagick.Formats;
using ImageMagick.ImageOptimizers;
using UnityEngine;

namespace io.wispforest.textureswapper.utils;

public class ImageUtils {
    public static byte[] getInitialFrameOfAnimatedImage(byte[] bytes) {
        return convertAnimatedImageToSlices(bytes, true).pngImageSlices[0];
    }
    
    public static PNGImageSequence convertAnimatedImageToSlices(byte[] bytes, bool onlySingleSlice = false, Identifier? id = null) {
        using var collection = new MagickImageCollection(bytes);

        collection.Coalesce();

        var pngByteArrays = new List<byte[]>(collection.Count);
        var deltaBetweenFrames = new List<float>();

        for (var i = 0; i < collection.Count; i++) {
            var image = collection[i];
            
            // Create a MemoryStream to store the PNG data.
            using MemoryStream memoryStream = new MemoryStream();

            // Ensure alpha channel is set.
            if (image.HasAlpha) image.Alpha(AlphaOption.Set); 
                        
            // Save the frame as a PNG to the MemoryStream.
            image.Write(memoryStream, new JpegWriteDefines());

            memoryStream.Position = 0;
            
            new ImageOptimizer() {
                    OptimalCompression = true
            }.Compress(memoryStream);

            memoryStream.Position = 0;
            
            if (onlySingleSlice) {
                return new PNGImageSequence([memoryStream.ToArray()], [], 0);
            }
            
            // Save the converted image slice to PNG
            pngByteArrays.Add(memoryStream.ToArray());
            
            deltaBetweenFrames.Add(image.AnimationDelay / 100f);
        }

        var sizeOfSequence = pngByteArrays
                .Select(pngBytes => pngBytes.Length)
                .Sum();
        
        Plugin.logIfDebugging(source => source.LogInfo($"Size of given computed image sequence [{(id?.ToString() ?? "unknown")}] is: {(sizeOfSequence / 1000000f)} mbs"));

        var maxAnimationTime = deltaBetweenFrames.Sum();

        return new PNGImageSequence(pngByteArrays, deltaBetweenFrames, maxAnimationTime);
    }
    
    public static byte[]? convertToWebm(Identifier id, byte[] inBytes) {
        //using var memoryStream = new MemoryStream();
        
        if (inBytes.Length <= 0) {
            throw new Exception("Fuck");
        }

        var filePath = Path.Combine(Plugin.TempVideoStoragePath, id.Namespace, id.Path);
        
        File.WriteAllBytes(filePath + ".gif", inBytes);
        
        var bl = FFMpegArguments.FromFileInput(
                        new FileInfo(filePath + ".gif"),
                        options => options.ForceFormat("gif")
                )
                .OutputToFile(filePath + ".webm",
                        overwrite: true,
                        options => options.ForceFormat("webm")
                                .WithConstantRateFactor(10)
                                .ForcePixelFormat("yuv420p")
                                .WithVideoBitrate(2200)
                                .WithVideoCodec("libvpx") // libvpx-
                                .WithAudioCodec("libvorbis") // libvorbis
                )
                .ProcessAsynchronously();
        
        bl.Wait();
        
        //throw new Exception("Test");

        if (bl.Result) {
            var outBytes = File.ReadAllBytes(filePath + ".webm");
            
            File.Delete(filePath + ".gif");
            File.Delete(filePath + ".webm");

            return outBytes;
        }
        
        return null;

        // using var collection = new MagickImageCollection(bytes);
        //
        // collection.Coalesce();
        //
        // using var memoryStream = new MemoryStream();
        //
        // collection.Write(memoryStream, new CustomWriteDefines());
        //
        // return memoryStream.ToArray();
    }

    private class CustomWriteDefines : IWriteDefines {
        public IEnumerable<IDefine> Defines => [new MagickDefine("webm:codec", "libvpx")];
        public MagickFormat Format => MagickFormat.WebM;
    }
}

public class PNGImageSequence(List<byte[]> pngImageSlices, List<float> deltaBetweenFrames, float maxAnimationTime) {
    public List<byte[]> pngImageSlices { get; } = pngImageSlices;
    public List<float> deltaBetweenFrames { get; } = deltaBetweenFrames;
    public float maxAnimationTime { get; } = maxAnimationTime;

    public List<Texture2D> getMaterials(Identifier id) {
        if (!Plugin.isMainThread()) {
            throw new Exception($"Unable to get materials as its not on main thread! [ID: {id}]");
        }
        
        return pngImageSlices
                .Select((bytes, i) => MaterialUtils.loadTextureFromBytes(bytes, id, $"_{i}"))
                .Where(material => material is not null)
                .ToList();
    }

    public TextureSequence convertToMaterialSequence(Identifier id) {
        var materials = getMaterials(id);

        return new TextureSequence(materials, deltaBetweenFrames, maxAnimationTime);
    }
}

public class TextureSequence(List<Texture2D> imageSlices, List<float> deltaBetweenFrames, float maxAnimationTime) {
    public List<Texture2D> imageSlices { get; } = imageSlices;
    public List<float> deltaBetweenFrames { get; } = deltaBetweenFrames;
    public float maxAnimationTime { get; } = maxAnimationTime;

    public void invalidateTextures() {
        foreach (var imageSlice in imageSlices) {
            MaterialUtils.invalidateTexture(imageSlice);
        }
    }
}