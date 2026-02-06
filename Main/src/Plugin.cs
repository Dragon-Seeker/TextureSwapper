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
using io.wispforest.endec;
using io.wispforest.endec.format.newtonsoft;
using io.wispforest.endec.impl;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.api;
using io.wispforest.textureswapper.api.components;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.api.target;
using io.wispforest.textureswapper.api.tooltip;
using io.wispforest.textureswapper.patches;
using io.wispforest.textureswapper.patches.ui;
using io.wispforest.textureswapper.utils;
using KeybindLib.Classes;
using REPOLib.Modules;
using Sirenix.Utilities;
using Steamworks;
using UnityEngine.SceneManagement;

namespace io.wispforest.textureswapper;

[BepInDependency("MediaHelper")]
[BepInDependency("bulletbot.keybindlib")]
[BepInPlugin(SwapperPluginInfo.PLUGIN_GUID, SwapperPluginInfo.PLUGIN_NAME, SwapperPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
   
   internal static string id = "texture_swapper";
   
   internal static string baseFolder => id;

   internal static string queriesFolder => "queries";
   internal static string targetsFolder => "targets";
   
   internal static readonly List<string> RAW_NAMES = ["texture_swapper_queries", "painting_swapper_images", "RandomPaintingSwap_Images", "CustomPaintings"];
   
   private static Plugin? _INSTANCE = null;
   private static ManualLogSource? _LOGGER = null;
   private static ConfigInstance? _CONFIG_ACCESS = null;

   private static Thread? _MAIN_THREAD = null;

   private readonly Harmony _harmony = new(SwapperPluginInfo.PLUGIN_GUID);

   internal static Plugin Instance => getOrThrow(_INSTANCE, $"{SwapperPluginInfo.PLUGIN_NAME} _instance");
   internal static new ManualLogSource Logger => getOrThrow(_LOGGER, $"{SwapperPluginInfo.PLUGIN_NAME} _logger");
   internal static ConfigInstance config => getOrThrow(_CONFIG_ACCESS, $"{SwapperPluginInfo.PLUGIN_NAME} _config_access");

   internal static ActiveSwapperStats? prevHolder { get; set; } = null;

   internal static string TempStoragePath => Path.Combine(Application.temporaryCachePath, "painting_swapper");

   internal static string TempVideoStoragePath => Path.Combine(TempStoragePath, "videos");
   internal static string TempImageStoragePath => Path.Combine(TempStoragePath, "images");
   internal static string TempAudioStoragePath => Path.Combine(TempStoragePath, "audio");

   private static T getOrThrow<T>(T? value, String fieldName) {
      if (value is not null) return value;

      throw new NullReferenceException($"Unable to get {fieldName} as it has not been initialized yet.");
   }

   public static void logIfDebugging(Action<ManualLogSource> logAction, Getter<bool>? predicate = null) {
      if (config.enableDebugLogging() && (predicate?.Invoke() ?? true)) logAction(Logger);
   }

   public static void logIfDebugging(Getter<string> message, Getter<bool>? predicate = null) {
      if (config.enableDebugLogging() && (predicate?.Invoke() ?? true)) Logger.LogInfo(message());
   }

   public static bool isMainThread() {
      return Thread.CurrentThread == _MAIN_THREAD;
   }

   private static MonoEvent _EVENTS;
   
   internal static MonoEvent Events => getOrThrow(_EVENTS, $"{SwapperPluginInfo.PLUGIN_NAME} _instance");
   
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
      
      var obj = new GameObject("TextureSwapperEventHolder");
      obj.hideFlags = HideFlags.HideAndDontSave;
      DontDestroyOnLoad(obj);
      
      _EVENTS = obj.AddComponent<MonoEvent>();

      Events.onUpdateCallback += _ => onUpdate();

      // Setup config access for later
      _CONFIG_ACCESS = new ConfigInstance(this, Config);

      base.Logger.LogInfo($"{SwapperPluginInfo.PLUGIN_NAME} has begun setup...");

      logIfDebugging(source => source.LogError($"Using the following path to store files temporarily: " + TempStoragePath));

      FileUtils.deleteOldFiles(TempStoragePath, 2);

      string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
      
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
      PhotonUnityEndecAddon.init(tuple => {
         if (tuple.addedProperly) {
            Logger.LogWarning(tuple.message);
         } else {
            Logger.LogError(tuple.message);
         }
      });

      // Hook into Wrapper Prefab pool so we can manipulate game objects after instantiation
      PrefabInstantiationEvent.onPrefabInstantiation += (gameObject, _, _, _) => SwapperComponentSetupUtils.commonSide(gameObject);
      
      // ObjectInstantiateEvent.onObjectInstantiation += (o, position, rotation) => {
      //    if (o is GameObject go) {
      //       if (!SemiFunc.IsMultiplayer() && hasLoadedQueries) {
      //          SwapperComponentSetupUtils.commonSide(go);
      //       }
      //       
      //       var renderers = go.GetComponentsInChildren<MeshRenderer>();
      //
      //       if (renderers == null) return;
      //       
      //       MeshRendererCache.getOrCreate().addRenderers(new List<MeshRenderer>(renderers));
      //    } else if (o is MeshRenderer meshRenderer) {
      //       MeshRendererCache.getOrCreate().addRenderer(meshRenderer);
      //    }
      // };

      SceneManager.sceneLoaded += (_, _) => MeshRendererCache.getOrCreate().getRenderers(refreshRenderers: true);

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

   private static bool? _IS_FUNNY_PERSON;
   
   public static bool isFunnyPerson  {
      get {
         if (!config.funny()) return false;
         
         if (_IS_FUNNY_PERSON == null) {
            if (SteamClient.IsValid) {
               _IS_FUNNY_PERSON = SteamClient.SteamId.Value == 76561198385078416;
         
               Logger.LogInfo($"Current User SteamID: {SteamClient.SteamId.Value}");
            }
         }

         return _IS_FUNNY_PERSON ?? false;
      }
   }

   public static Keybind refreshClientData { get; private set; }
   
   public delegate void AdditionalQueryLookup(Action<Identifier, IList<MediaQuery>> addCallback);

   public static event AdditionalQueryLookup ADDITIONAL_QUERY_LOOKUP;

   private static readonly List<string> validSwapperDataDirectories = [];
   private static readonly List<string> validQueryDataDirectories = [];

   private static readonly Dictionary<Identifier, List<TargetInstance>> swapperTargets = [];

   private static IList<TargetInstance>? mergedInstances;

   internal static IList<TargetInstance> getSwapperTargets() {
      return mergedInstances ??= swapperTargets.Values.SelectMany(ids => ids).ToImmutableList();
   }
   
   public void Start() {
      var pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

      Logger.LogInfo($"Plugin {SwapperPluginInfo.PLUGIN_NAME} starting!");
      
      // -- Load Legacy folders and then add them to query path
      {
         List<string> directories = [];
         
         directories.AddRange(config.directoryLocations());
         directories.Add(Path.Combine(Paths.ConfigPath, "texture_swapper_queries"));

         // --- Look into Other plugins folders for images
         directories.AddRange(
            Directory.GetDirectories(Path.GetDirectoryName(pluginFolder)!)
               .SelectMany(directory => RAW_NAMES.Select(s => Path.Combine(directory, s)))
         );

         validQueryDataDirectories.AddRange(directories.Where(Directory.Exists));
      }
      
      // -- Load base data folders
      {
         List<string> directories = [];
         
         directories.AddRange(config.directoryLocations().Select(s => Path.Combine(s, baseFolder)));
         directories.Add(Path.Combine(Paths.ConfigPath, baseFolder));

         // --- Look into Other plugins folders for images
         directories.AddRange(
            Directory.GetDirectories(Path.GetDirectoryName(pluginFolder)!)
               .Select(directory => Path.Combine(directory, baseFolder))
         );
         
         validSwapperDataDirectories.AddRange(directories.Select(s => {
            try {
               if (Directory.Exists(s)) Directory.CreateDirectory(s);
            } catch (Exception e) { }

            return s;
         }));
      }
      
      setupDataAndDirectories();
      
      //--
      
      if (config.funny()) {
         ADDITIONAL_QUERY_LOOKUP += callback => callback(StaticWebQueryType.ID, [StaticWebQuery.of(Identifier.of(id, "very_funny_mode_image"), ["https://i.imgur.com/0GTrjm7.jpeg"])]);
      }
      
      // --
      
      Logger.LogInfo($"Plugin {SwapperPluginInfo.PLUGIN_NAME} started Successfully!");
   }

   internal void setupDataAndDirectories(bool handleFolderCreation = true) {
      swapperTargets.Clear();
      
      foreach (var directory in validSwapperDataDirectories) {
         var targetDir = Path.Combine(directory, targetsFolder);
         
         if (handleFolderCreation) {
            var queryDir = Path.Combine(directory, queriesFolder);

            try {
               if (!Directory.Exists(queryDir)) Directory.CreateDirectory(queryDir);
            } catch (Exception e) { }
         
            validQueryDataDirectories.Add(queryDir);
         
            try {
               if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            } catch (Exception e) { }
         }
         
         if (!Directory.Exists(targetDir)) continue;
         
         List<string> jsonFiles = [..Directory.GetFiles(targetDir, "*.json"), ..Directory.GetFiles(targetDir, "*.json5")];
         
         foreach (var jsonFile in jsonFiles) {
            try {
               var targetInstances = JsonUtils.parseFromFile(jsonFile, TargetInstancePack.ENDEC);

               if (targetInstances == null) continue;
                  
               swapperTargets.computeIfAbsent(targetInstances.id, () => [])
                  .AddRange(targetInstances.instances);
            } catch (Exception e) {
               Logger.LogError($"Unable to parse the given file [{jsonFile}] as TargetInstances: {e}");
            }
         }
      }
      
      mergedInstances = null;
   }

   internal static Identifier funnyId = Identifier.ofUri("https://i.imgur.com/0GTrjm7.jpeg");

   private bool hasLoadedQueries;

   private readonly Dictionary<Identifier, IList<MediaQuery>> dynamicTypeToQueries = new();

   public static readonly LevelPredicate VALID_PREDICATE = LevelUtils.of(
      Operation.NONE, 
      m => m.levelMainMenu, 
      m => m.levelSplashScreen, 
      m => m.levelTutorial
   );
   
   // TODO: DOSE NOT WORK IN SINGLE PLAYER FOR SOME FUCKING REASON GOD DAM IT
   internal void loadQueries(string? levelName = null) {
      if (hasLoadedQueries) return;

      if (levelName != null) {
         RunManager? manager = RunManager.instance;
         var level = (manager?.levels ?? []).FirstOrDefault(level => level.name == levelName);
      
         if (level == null || !VALID_PREDICATE(manager!, level)) return;
      }

      Logger.LogInfo($"Attempting Texture Swapper Loading!");
      
      //--

      var typeToQueries = new Dictionary<Identifier, IList<MediaQuery>>();

      ADDITIONAL_QUERY_LOOKUP?.Invoke((identifier, list) => { typeToQueries.computeIfAbsent(identifier, _ => new List<MediaQuery>()).addAll(list); });

      var queryEntriesEndec = StructEndecBuilder.of(
            MediaQueryTypeRegistry.GROUPED_QUERY_DATA.optionalFieldOf<QueryEntries>("query_entries", pair => pair.queries, () => new Dictionary<Identifier, IList<MediaQuery>>()),
            MediaQueryTypeRegistry.GROUPED_QUERY_DATA.optionalFieldOf<QueryEntries>("dynamic_query_entries", pair => pair.queries, () => new Dictionary<Identifier, IList<MediaQuery>>()),
            (queries, dynamicQueries) => new QueryEntries(queries, dynamicQueries));
      
      Task.Run(() => {
         foreach (var directory in validQueryDataDirectories) {
            try {
               typeToQueries.computeIfAbsent(LocalMediaQueryType.ID, _ => []).Add(LocalMediaQuery.ofDirectory(null, directory));
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
         
         runQueries("alternative_censor_images", UserSettings.data.alternativeCensorImages);
         runQueries("static_queries", typeToQueries);
         runQueries("dynamic_queries", dynamicTypeToQueries);
      });

      Logger.LogInfo($"Queued up all loading for Texture Swapper!");

      hasLoadedQueries = true;
   }

   private int pastLevelsCompleted = 0;
   private ISet<MediaQueryKey> oldDynamicQueries = new HashSet<MediaQueryKey>();
   private bool mustLoadQueriesFirst = true;
   
   internal static int funnyChance = 3;
   
   private void handleDynamicQueries(int levelsCompleted, string level) {
      int maxLevelWait = config.dynamicQueriesLevelCount();

      funnyChance = Math.Min(100, funnyChance * 2);

      if (levelsCompleted == 0) {
         foreach (var key in oldDynamicQueries) {
            MediaSwapperStorage.removeMediaWithGuid(key);
         }

         oldDynamicQueries.Clear();

      } else {
         var levelDifference = levelsCompleted - pastLevelsCompleted;

         if (levelDifference >= maxLevelWait - 1 && mustLoadQueriesFirst) {
            Dictionary<Identifier, IList<MediaQuery>> newDynamicTypeToQueries = new();

            dynamicTypeToQueries.forEach((identifier, list) => {
               newDynamicTypeToQueries[identifier] = list.Select(query => {
                  oldDynamicQueries.Add(query.key);

                  return query.copy();
               }).ToList();
            });

            runQueries("dynamic_queries", newDynamicTypeToQueries);

            mustLoadQueriesFirst = false;
         } else if (levelDifference >= maxLevelWait) {
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

   private void onUpdate() {
      MediaSwapperStorage.handleToBeStoredHandlers();
      MainThreadHelper.handleActionsOnMainThread();
      ImageSequenceHolder.actIfPresent(holder => holder.checkIfMaterialsLoaded());
      MultiThreadHelper.INSTANCE.pruneTasks();
      DebugTooltipInfo.update();
      BasicTooltipInfo.update();

      if (SemiFunc.InputDown(refreshClientData.inputKey)) {
         setupDataAndDirectories(false);
         
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

public class TargetInstancePack(Identifier id, IList<TargetInstance> instances) {
   
   public static readonly Endec<TargetInstancePack> ENDEC = StructEndecBuilder.of(
      Identifier.ENDEC.fieldOf<TargetInstancePack>("id", s => s.id),
      TargetInstance.ENDEC.listOf().fieldOf<TargetInstancePack>("targets", s => s.instances),
      (id, list) => new (id, list)
   );
   
   public Identifier id { get; } = id;
   public IList<TargetInstance> instances { get; } = instances;
}
