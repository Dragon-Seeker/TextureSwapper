using System.IO;
using io.wispforest.textureswapper.api;
using Unity.VisualScripting;
using UnityEngine;
using Material = UnityEngine.Material;

namespace io.wispforest.textureswapper.utils;

public class MaterialUtils {
    
    public static Texture2D? loadTextureFromBytes(byte[] fileData, Identifier id, string postFix = "") {
        Texture2D texture = new Texture2D(2, 2);
        
        if(texture.LoadImage(fileData)) {
            texture.Apply();
            
            Plugin.logIfDebugging(source => source.LogInfo($"Texture created from image: {id}{postFix}"));
        } else {
            Plugin.logIfDebugging(source => source.LogError($"Unable to load the given image: {id}{postFix}"));
            return null;
        }

        return texture;
    }
    
    public static Material? loadMaterialFromBytes(RawMediaData rawMediaData) {
        return loadMaterialFromBytes(rawMediaData.getBytes(), rawMediaData.id);
    }
    
    /**
    * Loads a texture from a png file into memory
    */
    public static Material? loadMaterialFromBytes(byte[] fileData, Identifier id, string postFix = "") {
        var texture = loadTextureFromBytes(fileData, id, postFix);

        return texture is null ? null : createMaterial(texture, id, postFix);
    }
    
    public static Material createMaterial(Texture2D? texture, Identifier id, string postFix = "") {
        Plugin.logIfDebugging(source => source.LogInfo($"Image loaded and Material created: {id}{postFix}"));

        var material = new Material(Shader.Find("Standard"));

        if(texture is not null) material.mainTexture = texture;
        
        return material;
    }

    public static void invalidateMaterial(Material material) {
        var texture = material.mainTexture;
        
        invalidateTexture(texture);
        
        if (!material.IsDestroyed()) Object.Destroy(material);
    }
    
    public static void invalidateTexture(Texture texture) {
        if (!texture.IsDestroyed()) Object.Destroy(texture);
    }
}