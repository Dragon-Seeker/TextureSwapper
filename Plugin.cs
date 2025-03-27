using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BepInEx.Configuration;
using ImageMagick;
using JetBrains.Annotations;
using MonoMod.Utils;
using Unity.VisualScripting;

namespace RandomPaintingSwap;

[BepInPlugin(MODID, NAME, VERSION)]
public class Plugin : BaseUnityPlugin {
    public const string MODID = "io.wispforest.painting_swapper";
    public readonly List<string> RAW_NAMES = ["painting_swapper_images", "RandomPaintingSwap_Images", "CustomPaintings"];
    public const string NAME = "Painting Swapper";
    public const string VERSION = "1.0.0";

    public static Plugin Instance { get; private set; }
    
    internal static new ManualLogSource Logger;
    
    // Target materials
    public static readonly List<string> DEFAULT_TEXTURE_TARGETS = ["Painting_H_Landscape", "Painting_V_Furman", "painting teacher01", "painting teacher02", "painting teacher03", "painting teacher04", "Painting_S_Tree" ];
    // Image file pattern
    public static readonly HashSet<string> imagePatterns = ["*.png", "*.jpg", "*.jpeg", "*.gif"];

    private readonly Harmony harmony = new (MODID);
    
    // List of loaded materials
    public Dictionary<string, Material> loadedMaterials = [];

    private ConfigEntry<String> PICTURE_TEXTURE_TARGETS;
    
    private ConfigEntry<String> DIRECTORY_LOCATION;
    private ConfigEntry<String> PHOTO_LOCATION;
    
    public List<String> PictureTextureTargets() => new (PICTURE_TEXTURE_TARGETS.Value.Split(','));
    
    public List<String> DirectoryLocations() => new (DIRECTORY_LOCATION.Value.Split(','));
    public List<String> PhotoLocations() => new (PHOTO_LOCATION.Value.Split(','));
    
    /**
     * Init Plugin
     */
    private async void Awake() {
        MagickNET.Initialize();
        
        Instance = this;

        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {NAME} is loaded!");

        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Config.Bind("Test", "Test", false, new ConfigDescription("Testing Stuff"));
        
        PICTURE_TEXTURE_TARGETS = Config.Bind("Paintings", "PaintingTextures", DEFAULT_TEXTURE_TARGETS.Join(delimiter: ","), new ConfigDescription("All texture targets to replace with custom images"));
        
        DIRECTORY_LOCATION = Config.Bind("Photos", "DirectoryLocations", "", new ConfigDescription("Location of all directories to be looked at for images"));
        PHOTO_LOCATION = Config.Bind("Photos", "RawImageLocations", "", new ConfigDescription("Location of all photos to be downloaded"));
        
        //Config.Save();
        
        var directories = new List<String>();
        
        string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        string pluginsStorageArea = Path.GetDirectoryName(pluginDirectory);
        
        foreach (var directory in Directory.GetDirectories(pluginsStorageArea)) {
            foreach (var rawName in RAW_NAMES) {
                var possibleImageDirectory = Path.Combine(directory, rawName);

                if (Directory.Exists(directory)) {
                    directories.Add(possibleImageDirectory);
                }
            }
        }
        
        directories.Add(Path.Combine(pluginDirectory, RAW_NAMES[0]));
        directories.AddRange(DirectoryLocations());
        
        CreateImagesDirectory(directories);
        await LoadImagesFromDirectoryAndWeb(directories);
    }

    /**
     * Create an "IMAGE_FOLDER_NAME" folder if it doesn't exist
     */
    private void CreateImagesDirectory(List<String> directories) {
        foreach (var directory in directories) {
            try {
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                    Logger.LogInfo($"Folder {directory} created successfully!");
                } else {
                    Logger.LogInfo($"Folder {directory} detected!)");
                }
            } catch (Exception e) {
                Logger.LogError($"Unable to create directory [{directory}] for the given reason:");
                Logger.LogError(e);
            }
        }
    }

    /**
     * Load images from the "IMAGE_FOLDER_NAME" folder
     */
    private async Task LoadImagesFromDirectoryAndWeb(List<String> directories) {
        Logger.LogInfo($"Loading images from directory [{directories}]");
        foreach (var directory in directories) {
            if (!Directory.Exists(directory)) {
                Logger.LogWarning($"The folder {directory} does not exist!");
                continue;
            }

            List<string> directoryimageFiles = imagePatterns.SelectMany(pattern => Directory.GetFiles(directory, pattern)).ToList();

            if (!directoryimageFiles.Any()) {
                Logger.LogWarning($"No images found in the folder {directory}");
                continue;
            }
            
            loadedMaterials.AddRange<KeyValuePair<string, Material>>(
                directoryimageFiles
                    .Select(imageFile => LoadTextureFromFile(imageFile))
                    .Where(entry => entry != null)
                    .Select(entry => entry.Value!)
                    .ToList()
                );
        }
        
        Logger.LogInfo($"Loading images from the web: [{PhotoLocations()}]");
        
        loadedMaterials.AddRange(await LoadImagesFromTheWeb(PhotoLocations()));
        
        Logger.LogInfo($"Total Images : {loadedMaterials}");
    }

    [CanBeNull]
    private Material CreateMaterial(string fileURI, [CanBeNull] Texture2D texture) {
        if (texture == null) return null;
        
        Logger.LogInfo($"Image loaded and Material created: {Path.GetFileNameWithoutExtension(fileURI)}");
        
        return new Material(Shader.Find("Standard")) { mainTexture = texture };
    }

    private KeyValuePair<string, Material>? LoadTextureFromFile(string filePath) {
        try {
            byte[] fileData = File.ReadAllBytes(filePath);

            return LoadTextureFromBytes(filePath, fileData);
        } catch (Exception e) {
            Logger.LogError($"An error has a occured when trying to read image file locally: {e.Message}");
        }
        
        return null;
    }

    /**
     * Loads a texture from a png file into memory
     */
    [CanBeNull]
    private KeyValuePair<string, Material>? LoadTextureFromBytes(string fileURI, byte[] fileData) {
        Texture2D texture = new Texture2D(2, 2);

        if (texture.LoadImage(fileData)) {
            texture.Apply();
        } else {
            Logger.LogError($"Unable to load the given image: {fileURI}");
            return null;
        }
        
        return KeyValuePair.Create(Path.GetFileName(fileURI), CreateMaterial(fileURI, texture));
    }

    private async Task<Dictionary<string, Material>> LoadImagesFromTheWeb(List<string> images) {
        Dictionary<string, Material> loadedMaterials = new Dictionary<string, Material>();
        
        using (var httpClient = new HttpClient()) {
            foreach (var entry in CategorizeUrls(images)) {
                var urls = entry.Value;
                var type = entry.Key;
                
                foreach (var imageUrl in urls) {
                    try {
                        byte[] imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

                        KeyValuePair<string, Material>? textureData = null;
                        
                        if (type.Equals("Other")) {
                            try {
                                using (var converter = new MagickImage(imageBytes)) {
                                    converter.Format = MagickFormat.Png;
                    
                                    using (var memoryStream = new MemoryStream()) {
                                        converter.Write(memoryStream);

                                        textureData = LoadTextureFromBytes(imageUrl, memoryStream.ToArray());
                                    }
                                }
                            } catch (MagickException magickEx) {
                                Logger.LogError($"Magick.NET Error: {magickEx.Message}");
                            } 
                        } else {
                            textureData = LoadTextureFromBytes(imageUrl, imageBytes);
                        }

                        if (textureData == null) {
                            Logger.LogError($"Unable to load the image {imageUrl}");
                        } else {
                            loadedMaterials.Add(textureData?.Key, textureData?.Value);
                        }
                    } catch (HttpRequestException httpEx)  {
                        Logger.LogError($"HTTP Request Error: {httpEx.Message}");
                    } catch (Exception ex)  {
                        Logger.LogError($"An error occurred: {ex.Message}");
                    }
                }
            }
        }

        return loadedMaterials;
    }
    
    public static Dictionary<string, List<string>> CategorizeUrls(List<string> urls) {
        var categories = new Dictionary<string, List<string>>();

        // Add a default category for non-image URLs
        categories.Add("Other", new List<string>());

        // Precompile regex patterns for efficiency
        var regexPatterns = imagePatterns.Select(pattern => new {
            Pattern = pattern,
            Regex = new Regex(Regex.Escape(pattern).Replace("\\*", ".*"), RegexOptions.IgnoreCase)
        }).ToList();

        foreach (var url in urls) {
            bool categorized = false;

            foreach (var regexPattern in regexPatterns) {
                if (regexPattern.Regex.IsMatch(url)) {
                    if (!categories.ContainsKey(regexPattern.Pattern)) {
                        categories[regexPattern.Pattern] = new List<string>();
                    }
                    categories[regexPattern.Pattern].Add(url);
                    categorized = true;
                    break;
                }
            }

            if (!categorized) categories["Other"].Add(url);
        }

        return categories;
    }

    [HarmonyPatch(typeof(LoadingUI), "LevelAnimationComplete")]
    public class PatchLoadingUI {
        
        /**
         * Replacing base images with plugin images
         */
        [HarmonyPostfix]
        private static void Postfix() {
            Logger.LogInfo("Replacing base images with plugin images");

            Scene activeScene = SceneManager.GetActiveScene();
            // List of all objects in the scene
            List<GameObject> list = activeScene.GetRootGameObjects().ToList();

            var materials = Instance.loadedMaterials.Values.ToList();
            var pictureTargets = Instance.PictureTextureTargets();
            
            // Traversing all objects in the scene
            foreach (GameObject gameObject in list) {
                // Traversing all MeshRenderers of the object
                foreach (MeshRenderer mesh in gameObject.GetComponentsInChildren<MeshRenderer>()) {
                    // Storing the shared materials of the MeshRenderer
                    Material[] sharedMaterials = mesh.sharedMaterials;

                    if (sharedMaterials == null) continue;

                    // Traversing all shared materials of the MeshRenderer
                    for (int i = 0; i < sharedMaterials.Length; i++) {
                        Material material = sharedMaterials[i];
                        if (material != null && pictureTargets.Contains(material.name) && materials.Count() > 0) {
                            //Logger.LogInfo($"---------------------------> {material.name}");
                            
                            sharedMaterials[i] = materials[UnityEngine.Random.Range(0, materials.Count)];
                        }
                    }

                    // Applying custom materials
                    mesh.sharedMaterials = sharedMaterials;
                }
            }
        }
    }
}
