using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using ImageMagick;
using io.wispforest.format.binary;
using JetBrains.Annotations;
using MonoMod.Utils;
using io.wispforest.textureswapper.api;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.endec.format.newtonsoft;
using io.wispforest.textureswapper.patches;
using io.wispforest.textureswapper.utils;
using Photon.Pun;
using Sirenix.Utilities;
using Unity.VisualScripting;
using Chainloader = BepInEx.Bootstrap.Chainloader;

namespace io.wispforest.textureswapper;

[BepInPlugin(SwapperPluginInfo.PLUGIN_GUID, SwapperPluginInfo.PLUGIN_NAME, SwapperPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
   internal static readonly List<string> RAW_NAMES = [ "painting_swapper_images",  "RandomPaintingSwap_Images",  "CustomPaintings" ];

   private static Plugin? _INSTANCE = null;
   private static ManualLogSource? _LOGGER = null;
   private static ConfigAccess? _CONFIG_ACCESS = null;
   
   private static Thread? _MAIN_THREAD = null;
   private readonly Harmony _harmony = new (SwapperPluginInfo.PLUGIN_GUID);

   internal static Plugin Instance => getOrThrow(_INSTANCE, $"{SwapperPluginInfo.PLUGIN_NAME} _instance");
   internal static ManualLogSource Logger => getOrThrow(_LOGGER, $"{SwapperPluginInfo.PLUGIN_NAME} _logger");
   internal static ConfigAccess ConfigAccess => getOrThrow(_CONFIG_ACCESS, $"{SwapperPluginInfo.PLUGIN_NAME} _config_access");

   internal static string TempStoragePath => Path.Combine(Application.temporaryCachePath, "painting_swapper");
   
   internal static string TempVideoStoragePath => Path.Combine(TempStoragePath, "videos");
   internal static string TempImageStoragePath => Path.Combine(TempStoragePath, "images");
   internal static string TempAudioStoragePath => Path.Combine(TempStoragePath, "audio");

   private static T getOrThrow<T>(T? value, String fieldName) {
      if(value is not null) return value;
         
      throw new NullReferenceException($"Unable to get {fieldName} as it has not been initialized yet.");
   }
   
   public static void logIfDebugging(Action<ManualLogSource> logAction, Func<bool>? predicate = null) {
      if(ConfigAccess.debugLogging() && (predicate?.Invoke() ?? true)) logAction(Logger);
   }
   
   public static void logIfDebugging(Func<String> message, Func<bool>? predicate = null) {
      if(ConfigAccess.debugLogging()&& (predicate?.Invoke() ?? true)) Logger.LogInfo(message());
   }

   public static bool isMainThread() {
      return Thread.CurrentThread == _MAIN_THREAD;
   }
   
   /**
    * Init Plugin
    */
   private void Awake() {
      // TODO: MAYBE DO SO IN PATCH?
      // var maxViewIdsInfo = typeof(PhotonNetwork).GetField("MAX_VIEW_IDS", BindingFlags.Public | BindingFlags.Static);
      // if (maxViewIdsInfo is not null) {
      //    var newValue = 10000;
      //    maxViewIdsInfo.SetValue(null, newValue);
      //    if (PhotonNetwork.MAX_VIEW_IDS == newValue) {
      //       _LOGGER.LogInfo($"Adjusted the Photons MAX_VIEW_IDS to be max value of {newValue}");
      //    } else {
      //       _LOGGER.LogError($"Unable to adjust Photons MAX_VIEW_IDS meaning its capped at {PhotonNetwork.MAX_VIEW_IDS}, issues may exist!");
      //    }
      // } else {
      //    _LOGGER.LogError($"Unable to adjust Photons MAX_VIEW_IDS meaning its capped at {PhotonNetwork.MAX_VIEW_IDS}, issues may exist!");
      // }
      
      // -- Plugin startup logic
      
      _INSTANCE = this;
      
      // Setup config access for later
      _CONFIG_ACCESS = new ConfigAccess(Config);
      
      // Setup Logger for access outside the plugin context
      _LOGGER = base.Logger;
      base.Logger.LogInfo($"{SwapperPluginInfo.PLUGIN_NAME} has begun setup...");

      logIfDebugging(source => source.LogError($"Using the following path to store files temporarily: " + TempStoragePath));
      
      FileUtils.deleteOldFiles(TempStoragePath, 2);
      
      string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
      
      GlobalFFOptions.Configure(new FFOptions {
            BinaryFolder = pluginFolder, 
            //TemporaryFilesFolder = Path.Combine(TempVideoStoragePath, "ffmpeg_temp"),
            LogLevel = FFMpegLogLevel.Trace,
      });
      
      MagickNET.SetNativeLibraryDirectory(pluginFolder);
      
      var path = MagickNET.GetEnvironmentVariable("Path");
      if (!path?.Contains("pluginFolder") ?? false) {
         MagickNET.SetEnvironmentVariable("Path", @$"{path};{pluginFolder}");
      }
      
      // Prevent object from being garbage collected
      gameObject.hideFlags = HideFlags.HideAndDontSave;
      
      // Setup value for what is the main thread for later ability
      // to sync code calls that do not like async
      _MAIN_THREAD ??= Thread.CurrentThread;
      
      // Required to convert image formats that are not supported to PNG
      MagickNET.Initialize();
      
      MediaIdentifiers.initErrorImages(pluginFolder);
      
      // Similar patch but adding compatibility to RepoLib to hook into after they init there prefab pool
      _harmony.PatchAll(
            Chainloader.PluginInfos.ContainsKey("REPOLib") 
            ? typeof(RepoLibNetworkPrefabsPatch) 
            : typeof(RunManagerPatch));

      // Just incase to make sure Photon Endec compat is loaded
      PhotonEndecAddon.init();
      
      // Hook into Wrapper Prefab pool so we can manipulate game objects after instantiation
      WrapperPrefabPool.onPrefabInstantiation += (gameObject, _, _, _) => SwapperComponentSetupUtils.commonSide(gameObject);
      
      // Register builtin provided query types
      MediaQueryTypeRegistry.register<LocalMediaQueryType, LocalMediaQuery, LocalMediaQueryResult>(LocalMediaQueryType.INSTANCE);
      MediaQueryTypeRegistry.register<StaticWebQueryType, StaticWebQuery, StaticWebQueryResult>(StaticWebQueryType.INSTANCE);
      
      var typeToQueries = new Dictionary<Identifier, IList<MediaQuery>>();

      var staticConfigUrls = ConfigAccess.staticWebMedia;

      if (staticConfigUrls.Count > 0) {
         typeToQueries.Add(StaticWebQueryType.ID, [StaticWebQuery.of(staticConfigUrls)]);
      }
      
      // -- General Creation of Local Directories to search
      
      var directories = new List<String>();
      
      // --- Create Directories for the base plugin combined with any other possible location
      directories.Add(Path.Combine(pluginFolder, RAW_NAMES[0]));
      directories.AddRange(ConfigAccess.directoryLocations);
      directories.Add(Path.Combine(Paths.ConfigPath, "texture_swapper_queries"));

      CreateImagesDirectory(directories);

      // --- Look into Other plugins folders for images
      string pluginsFolder = Path.GetDirectoryName(pluginFolder);

      foreach(var directory in Directory.GetDirectories(pluginsFolder)) {
         foreach(var rawName in RAW_NAMES) {
            var possibleImageDirectory = Path.Combine(directory, rawName);

            if(Directory.Exists(directory)) {
               directories.Add(possibleImageDirectory);
            }
         }
      }

      var queryList = MediaQueryTypeRegistry.QUERY_DATA.listOf().structOf("query_entries");

      Task.Run(() => {
         foreach (var directory in directories) {
            try {
               typeToQueries.computeIfAbsent(LocalMediaQueryType.ID, _ => []).Add(LocalMediaQuery.ofDirectory(directory));
            }
            catch (Exception e) {
               Logger.LogError($"Unable to query local directory: {e}");
            }

            if (!Directory.Exists(directory)) continue;

            var jsonFiles = Directory.GetFiles(directory, "*.json");

            foreach (var jsonFile in jsonFiles) {
               try {
                  var queries = JsonUtils.parseFromFile(jsonFile, queryList);

                  if (queries == null) continue;

                  foreach (var mediaQuery in queries) {
                     typeToQueries.computeIfAbsent(mediaQuery.getQueryTypeId(), _ => new List<MediaQuery>()).Add(mediaQuery);
                  }
               }
               catch (Exception e) {
                  Logger.LogError($"Unable to parse the given file [{jsonFile}] as MediaQueries: {e}");
               }
            }
         }

         //MultiThreadHelper.run(() => { while (true) { } });
         
         foreach (var entry in typeToQueries) {
            // TODO: Change ability to regulate how many requests are possible for each type
            MultiThreadHelper.run(new SemaphoreIdentifier(Identifier.of("texture_swapper", "types"), maxCount: 1), () => {
               foreach (var mediaQuery in entry.Value) {
                  MediaQueryTypeRegistry.attemptToHandleQuery(mediaQuery);
               }
            });
         }
      });
      
      // --

      Logger.LogInfo($"Plugin {SwapperPluginInfo.PLUGIN_NAME} loaded Successfully!");
   }
   
   private void Update() {
      MediaSwapperStorage.handleToBeStoredHandlers();
      MainThreadHelper.handleActionsOnMainThread();
      ImageSequenceHolder.actIfPresent(holder => {
         holder.checkIfMaterialsLoaded();
      });
   }

   /**
    * Create an "IMAGE_FOLDER_NAME" folder if it doesn't exist
    */
   private void CreateImagesDirectory(List<String> directories) {
      foreach(var directory in directories) {
         try {
            if(!Directory.Exists(directory)) {
               Directory.CreateDirectory(directory);
               logIfDebugging(source => source.LogInfo($"Folder {directory} created successfully!"));
            } else {
               logIfDebugging(source => source.LogInfo($"Folder {directory} detected!)"));
            }
         } catch(Exception e) {
            logIfDebugging(source => {
               source.LogError($"Unable to create directory [{directory}]:");
               source.LogError(e);
            });
         }
      }
   }
}
