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
using io.wispforest.impl;
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
using Photon.Realtime;
using Sirenix.Utilities;
using Unity.VisualScripting;
using Chainloader = BepInEx.Bootstrap.Chainloader;
using Object = UnityEngine.Object;

namespace io.wispforest.textureswapper;

[BepInPlugin(SwapperPluginInfo.PLUGIN_GUID, SwapperPluginInfo.PLUGIN_NAME, SwapperPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
   internal static readonly List<string> RAW_NAMES = ["texture_swapper_queries", "painting_swapper_images", "RandomPaintingSwap_Images", "CustomPaintings"];

   private static Plugin? _INSTANCE = null;
   private static ManualLogSource? _LOGGER = null;
   private static ConfigAccess? _CONFIG_ACCESS = null;

   private static Thread? _MAIN_THREAD = null;
   private readonly Harmony _harmony = new(SwapperPluginInfo.PLUGIN_GUID);

   internal static Plugin Instance => getOrThrow(_INSTANCE, $"{SwapperPluginInfo.PLUGIN_NAME} _instance");
   internal static new ManualLogSource Logger => getOrThrow(_LOGGER, $"{SwapperPluginInfo.PLUGIN_NAME} _logger");
   internal static ConfigAccess ConfigAccess => getOrThrow(_CONFIG_ACCESS, $"{SwapperPluginInfo.PLUGIN_NAME} _config_access");

   internal static ActiveSwapperHolder? prevHolder { get; set; } = null;

   internal static string TempStoragePath => Path.Combine(Application.temporaryCachePath, "painting_swapper");

   internal static string TempVideoStoragePath => Path.Combine(TempStoragePath, "videos");
   internal static string TempImageStoragePath => Path.Combine(TempStoragePath, "images");
   internal static string TempAudioStoragePath => Path.Combine(TempStoragePath, "audio");

   private static T getOrThrow<T>(T? value, String fieldName) {
      if (value is not null) return value;

      throw new NullReferenceException($"Unable to get {fieldName} as it has not been initialized yet.");
   }

   public static void logIfDebugging(Action<ManualLogSource> logAction, Func<bool>? predicate = null) {
      if (ConfigAccess.debugLogging() && (predicate?.Invoke() ?? true)) logAction(Logger);
   }

   public static void logIfDebugging(Func<String> message, Func<bool>? predicate = null) {
      if (ConfigAccess.debugLogging() && (predicate?.Invoke() ?? true)) Logger.LogInfo(message());
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

      // Setup Logger for access outside the plugin context
      _LOGGER = base.Logger;

      _INSTANCE = this;

      // Setup config access for later
      _CONFIG_ACCESS = new ConfigAccess(Config, MetadataHelper.GetMetadata(this));

      var fieldInfo = typeof(BaseUnityPlugin).GetRuntimeFields().Where(info => {
         _LOGGER.LogError(info.Name);
         return info.Name.Contains("Config");
      }).First();
      fieldInfo?.SetValue(this, _CONFIG_ACCESS);

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

      LocalFiles.init();

      MediaIdentifiers.initErrorImages(pluginFolder);

      // Similar patch but adding compatibility to RepoLib to hook into after they init there prefab pool
      _harmony.PatchAll(Chainloader.PluginInfos.ContainsKey("REPOLib") ? typeof(RepoLibNetworkPrefabsPatch) : typeof(RunManagerAwakePatch));
      _harmony.PatchAll(typeof(SemiFuncPatch));
      _harmony.PatchAll(typeof(RunManagerPatch));

      // Just incase to make sure Photon Endec compat is loaded
      PhotonEndecAddon.init();

      // Hook into Wrapper Prefab pool so we can manipulate game objects after instantiation
      WrapperPrefabPool.onPrefabInstantiation += (gameObject, _, _, _) => SwapperComponentSetupUtils.commonSide(gameObject);

      LevelEvents.ON_CHANGE += this.handleDynamicQueries;

      Logger.LogInfo($"Plugin {SwapperPluginInfo.PLUGIN_NAME} loaded Successfully!");
   }

   public delegate void AdditionalQueryLookup(Action<Identifier, IList<MediaQuery>> addCallback);

   public static event AdditionalQueryLookup ADDITIONAL_QUERY_LOOKUP;

   private List<string> directoriesToBeLoaded = [];

   public void Start() {
      string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

      Logger.LogInfo($"Plugin {SwapperPluginInfo.PLUGIN_NAME} starting!");

      // -- General Creation of Local Directories to search

      var directories = new List<String>();

      // --- Create Directories for the base plugin combined with any other possible location
      directories.Add(Path.Combine(pluginFolder, RAW_NAMES[0]));
      directories.AddRange(ConfigAccess.directoryLocations);
      directories.Add(Path.Combine(Paths.ConfigPath, "texture_swapper_queries"));

      foreach (var directory in directories) {
         try {
            if (!Directory.Exists(directory)) {
               Directory.CreateDirectory(directory);
               logIfDebugging(source => source.LogInfo($"Folder {directory} created successfully!"));
            }
            else {
               logIfDebugging(source => source.LogInfo($"Folder {directory} detected!)"));
            }
         }
         catch (Exception e) {
            logIfDebugging(source => {
               source.LogError($"Unable to create directory [{directory}]:");
               source.LogError(e);
            });
         }
      }

      // --- Look into Other plugins folders for images
      string pluginsFolder = Path.GetDirectoryName(pluginFolder);

      foreach (var directory in Directory.GetDirectories(pluginsFolder)) {
         foreach (var rawName in RAW_NAMES) {
            var possibleImageDirectory = Path.Combine(directory, rawName);

            if (Directory.Exists(directory)) {
               directories.Add(possibleImageDirectory);
            }
         }
      }

      directoriesToBeLoaded = directories;

      hasLoadedQueries = false;

      // --

      Logger.LogInfo($"Plugin {SwapperPluginInfo.PLUGIN_NAME} started Successfully!");
   }

   private bool hasLoadedQueries = false;

   private readonly Dictionary<Identifier, IList<MediaQuery>> dynamicTypeToQueries = new();

   // TODO: DOSE NOT WORK IN SINGLE PLAYER FOR SOME FUCKING REASON GOD DAM IT
   internal void loadQueries(string? levelName = null) {
      if (hasLoadedQueries) return;

      if (levelName != null) {
         var manager = RunManager.instance;

         var isInvalidLevel = levelName.Equals(manager.levelMainMenu.name) 
                              || levelName.Equals(manager.levelSplashScreen.name)
                              || levelName.Equals(manager.levelTutorial.name);

         if (isInvalidLevel) return;
      }

      Logger.LogInfo($"Attempting Texture Swapper Loading!");

      var typeToQueries = new Dictionary<Identifier, IList<MediaQuery>>();

      ADDITIONAL_QUERY_LOOKUP?.Invoke((identifier, list) => { typeToQueries.computeIfAbsent(identifier, _ => new List<MediaQuery>()).addAll(list); });

      var queryEntriesEndec = StructEndecBuilder.of(
            MediaQueryTypeRegistry.GROUPED_QUERY_DATA.optionalFieldOf<QueryEntries>("query_entries", pair => pair.queries, () => new Dictionary<Identifier, IList<MediaQuery>>()),
            MediaQueryTypeRegistry.GROUPED_QUERY_DATA.optionalFieldOf<QueryEntries>("dynamic_query_entries", pair => pair.queries, () => new Dictionary<Identifier, IList<MediaQuery>>()),
            (queries, dynamicQueries) => new QueryEntries(queries, dynamicQueries));

      Task.Run(() => {
         foreach (var directory in directoriesToBeLoaded) {
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
                  var queries = JsonUtils.parseFromFile(jsonFile, queryEntriesEndec);

                  if (queries is null) continue;
                  
                  typeToQueries.merge(queries.queries);
                  dynamicTypeToQueries.merge(queries.dynamicQueries);
               }
               catch (Exception e) {
                  Logger.LogError($"Unable to parse the given file [{jsonFile}] as MediaQueries: {e}");
               }
            }
         }

         //MultiThreadHelper.run(() => { while (true) { } });
         
         runQueries("alternative_censor_images", UserSettingsAccess.getDefinedSettings().alternativeCensorImages);
         runQueries("static_queries", typeToQueries);
         runQueries("dynamic_queries", dynamicTypeToQueries);
      });

      Logger.LogInfo($"Queued up all loading for Texture Swapper!");

      hasLoadedQueries = true;
   }

   private int pastLevelsCompleted = 0;
   private System.Collections.Generic.ISet<Guid> oldDynamicQueries = new HashSet<Guid>();
   private bool mustLoadQueriesFirst = true;

   private void handleDynamicQueries(int levelsCompleted, string level) {
      int maxLevelWait = ConfigAccess.dynamicQueriesLevelCount();

      if (levelsCompleted == 0) {
         foreach (var guid in oldDynamicQueries) {
            MediaSwapperStorage.removeMediaWithGuid(guid);
         }

         oldDynamicQueries.Clear();

      }
      else {
         var levelDifference = levelsCompleted - pastLevelsCompleted;

         if (levelDifference >= maxLevelWait - 1 && mustLoadQueriesFirst) {
            Dictionary<Identifier, IList<MediaQuery>> newDynamicTypeToQueries = new();

            dynamicTypeToQueries.forEach((identifier, list) => {
               newDynamicTypeToQueries[identifier] = list.Select(query => {
                  oldDynamicQueries.Add(query.guid);

                  return query.createFrom();
               }).ToList();
            });

            runQueries("dynamic_queries", newDynamicTypeToQueries);

            mustLoadQueriesFirst = false;
         }
         else if (levelDifference >= maxLevelWait) {
            MediaSwapperStorage.removeMediaWithGuids(oldDynamicQueries);

            oldDynamicQueries.Clear();

            mustLoadQueriesFirst = true;

            pastLevelsCompleted = levelsCompleted;
         }
      }
   }

   internal static void runQueries(string entryName, IDictionary<Identifier, IList<MediaQuery>> typeToQueries) {
      if (typeToQueries.Count <= 0) return;

      Task.Run(() => {
         Logger.LogInfo($"Running Media Queries in Texture Swapper [{entryName}]");

         foreach (var entry in typeToQueries) {
            // TODO: Change ability to regulate how many requests are possible for each type
            MultiThreadHelper.run(new SemaphoreIdentifier(Identifier.of("texture_swapper", "types"), maxCount: 1), () => {
               foreach (var mediaQuery in entry.Value) {
                  if (!MediaQueryTypeRegistry.attemptToHandleQuery(mediaQuery)) {
                     Logger.LogError($"Unable to handle the given query: [Id: {entry.Key}]");
                  }
               }
            });
         }

         Logger.LogInfo($"Ran all queries, results are to be appearing within the future");
      });
   }

   private void Update() {
      MediaSwapperStorage.handleToBeStoredHandlers();
      MainThreadHelper.handleActionsOnMainThread();
      ImageSequenceHolder.actIfPresent(holder => { holder.checkIfMaterialsLoaded(); });
      MultiThreadHelper.INSTANCE.pruneTasks();
   }
}

public class QueryEntries(IDictionary<Identifier, IList<MediaQuery>> queries, IDictionary<Identifier, IList<MediaQuery>> dynamicQueries) {
   public IDictionary<Identifier, IList<MediaQuery>> queries { get; } = queries;
   public IDictionary<Identifier, IList<MediaQuery>> dynamicQueries { get; } = dynamicQueries;
}
