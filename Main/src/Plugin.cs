using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using FFMpegCore;
using FFMpegCore.Enums;
using HarmonyLib.Tools;
using ImageMagick;
using io.wispforest.endec.format.newtonsoft;
using io.wispforest.endec.impl;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.api;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.api.tooltip;
using io.wispforest.textureswapper.patches;
using io.wispforest.textureswapper.patches.ui;
using io.wispforest.textureswapper.utils;
using KeybindLib.Classes;
using REPOLib.Modules;
using UnityEngine.SceneManagement;

namespace io.wispforest.textureswapper;

[BepInDependency("MediaHelper")]
[BepInDependency("bulletbot.keybindlib")]
[BepInPlugin(SwapperPluginInfo.PLUGIN_GUID, SwapperPluginInfo.PLUGIN_NAME, SwapperPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
   internal static readonly List<string> RAW_NAMES = ["texture_swapper_queries", "painting_swapper_images", "RandomPaintingSwap_Images", "CustomPaintings"];
   
   private static Plugin? _INSTANCE = null;
   private static ManualLogSource? _LOGGER = null;
   private static ConfigInstance? _CONFIG_ACCESS = null;

   private static Thread? _MAIN_THREAD = null;

   private readonly Harmony _harmony = new(SwapperPluginInfo.PLUGIN_GUID);

   internal static Plugin Instance => getOrThrow(_INSTANCE, $"{SwapperPluginInfo.PLUGIN_NAME} _instance");
   internal static new ManualLogSource Logger => getOrThrow(_LOGGER, $"{SwapperPluginInfo.PLUGIN_NAME} _logger");
   internal static ConfigInstance config => getOrThrow(_CONFIG_ACCESS, $"{SwapperPluginInfo.PLUGIN_NAME} _config_access");

   internal static string id = "texture_swapper";

   internal static ActiveSwapperStats? prevHolder { get; set; } = null;

   internal static string TempStoragePath => Path.Combine(Application.temporaryCachePath, "painting_swapper");

   internal static string TempVideoStoragePath => Path.Combine(TempStoragePath, "videos");
   internal static string TempImageStoragePath => Path.Combine(TempStoragePath, "images");
   internal static string TempAudioStoragePath => Path.Combine(TempStoragePath, "audio");

   private static T getOrThrow<T>(T? value, String fieldName) {
      if (value is not null) return value;

      throw new NullReferenceException($"Unable to get {fieldName} as it has not been initialized yet.");
   }

   public static void logIfDebugging(Action<ManualLogSource> logAction, Func<bool>? predicate = null) {
      if (config.enableDebugLogging() && (predicate?.Invoke() ?? true)) logAction(Logger);
   }

   public static void logIfDebugging(Func<String> message, Func<bool>? predicate = null) {
      if (config.enableDebugLogging() && (predicate?.Invoke() ?? true)) Logger.LogInfo(message());
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
      _CONFIG_ACCESS = new ConfigInstance(this, Config);

      base.Logger.LogInfo($"{SwapperPluginInfo.PLUGIN_NAME} has begun setup...");

      logIfDebugging(source => source.LogError($"Using the following path to store files temporarily: " + TempStoragePath));

      FileUtils.deleteOldFiles(TempStoragePath, 2);

      string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

      // Prevent object from being garbage collected
      gameObject.hideFlags = HideFlags.HideAndDontSave;

      // Setup value for what is the main thread for later ability
      // to sync code calls that do not like async
      _MAIN_THREAD ??= Thread.CurrentThread;
      
      LocalFiles.init();

      MediaIdentifiers.initErrorImages(pluginFolder);
      
      _harmony.PatchAll(typeof(SemiFuncPatch));
      _harmony.PatchAll(typeof(RunManagerPatch));
      
      _harmony.PatchAll(typeof(DefaultPoolPatch));
      _harmony.PatchAll(typeof(UnityEngineObjectPatch));
      
      _harmony.PatchAll(typeof(EnergyUIPatch));
      _harmony.PatchAll(typeof(HealthUIPatch));
      
      if (Chainloader.PluginInfos.ContainsKey("bulletbot.keybindlib")) _harmony.PatchAll(typeof(KeybindsPatch));
      
      // Just incase to make sure Photon Endec compat is loaded
      PhotonEndecAddon.init();

      // Hook into Wrapper Prefab pool so we can manipulate game objects after instantiation
      PrefabInstantiationEvent.onPrefabInstantiation += (gameObject, _, _, _) => SwapperComponentSetupUtils.commonSide(gameObject);
      
      ObjectInstantiateEvent.onObjectInstantiation += (o, position, rotation) => {
         if (o is GameObject go) {
            if (!SemiFunc.IsMultiplayer() && hasLoadedQueries) {
               SwapperComponentSetupUtils.commonSide(go);
            }
            
            var renderers = go.GetComponentsInChildren<MeshRenderer>();

            if (renderers == null) return;
            
            MeshRendererCache.getOrCreate().addRenderers(new List<MeshRenderer>(renderers));
         } else if (o is MeshRenderer meshRenderer) {
            MeshRendererCache.getOrCreate().addRenderer(meshRenderer);
         }
      };

      SceneManager.sceneLoaded += (arg0, mode) => {
         MeshRendererCache.getOrCreate().getRenderers(refreshRenderers: true);
      };

      LevelEvents.ON_CHANGE += this.handleDynamicQueries;
      
      BasicTooltipInfo.init();
      DebugTooltipInfo.init();
      
      refreshClientData = Keybinds.Bind("Refresh", "Refresh Client Side Data", "<No Binding>");

      DebugErrors.decodeErrorHook = (o, exception) => {
         Plugin.logIfDebugging((logger) => {
            logger.LogError("Error occured within endec when decoding object: ");
            logger.LogError($"Object: {o.ToString()}");
            logger.LogError(exception);
         });
      };
      
      Logger.LogInfo($"Plugin {SwapperPluginInfo.PLUGIN_NAME} loaded Successfully!");
   }

   public static Keybind refreshClientData { get; private set; }
   
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
      directories.AddRange(config.directoryLocations());
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
         
         runQueries("alternative_censor_images", UserSettings.data().alternativeCensorImages);
         runQueries("static_queries", typeToQueries);
         runQueries("dynamic_queries", dynamicTypeToQueries);
      });

      Logger.LogInfo($"Queued up all loading for Texture Swapper!");

      hasLoadedQueries = true;
   }

   private int pastLevelsCompleted = 0;
   private ISet<Guid> oldDynamicQueries = new HashSet<Guid>();
   private bool mustLoadQueriesFirst = true;

   private void handleDynamicQueries(int levelsCompleted, string level) {
      int maxLevelWait = config.dynamicQueriesLevelCount();

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
            var typeId = entry.Key;

            SemaphoreIdentifier id = MediaQueryTypeRegistry.getTypeDyn(typeId).createSemaphoreIdentifier(true);
            
            MultiThreadHelper.run(id, () => {
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
      ImageSequenceHolder.actIfPresent(holder => holder.checkIfMaterialsLoaded());
      MultiThreadHelper.INSTANCE.pruneTasks();
      DebugTooltipInfo.update();
      BasicTooltipInfo.update();

      if (SemiFunc.InputDown(refreshClientData.inputKey)) {
         config.reloadPrimaryConfig();
         
         var rootObjects =  SceneManager.GetActiveScene().GetRootGameObjects();

         // Iterate through the root GameObjects and print their names.
         foreach (var rootObject in rootObjects) {
            if (!rootObject.name.Equals("Level Generator")) continue;

            SwapperComponentSetupUtils.unswapScene(rootObject);

            ActiveSwapperStats.getOrCreate().reset();

            SwapperComponentSetupUtils.commonSide(rootObject);
         }
      }
   }
}

public class QueryEntries(IDictionary<Identifier, IList<MediaQuery>> queries, IDictionary<Identifier, IList<MediaQuery>> dynamicQueries) {
   public IDictionary<Identifier, IList<MediaQuery>> queries { get; } = queries;
   public IDictionary<Identifier, IList<MediaQuery>> dynamicQueries { get; } = dynamicQueries;
}
