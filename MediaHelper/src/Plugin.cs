

using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using FFMpegCore;
using FFMpegCore.Enums;
using ImageMagick;
using UnityEngine;
using PluginInfo = io.wispforest.utils.PluginInfo;

namespace io.wispforest;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
   private static Plugin? _INSTANCE = null;
   private static ManualLogSource? _LOGGER = null;

   internal static Plugin Instance => getOrThrow(_INSTANCE, $"{PluginInfo.PLUGIN_NAME} _instance");
   internal static new ManualLogSource Logger => getOrThrow(_LOGGER, $"{PluginInfo.PLUGIN_NAME} _logger");

   internal static string id = "media_helper";

   private static T getOrThrow<T>(T? value, String fieldName) {
      if (value is not null) return value;

      throw new NullReferenceException($"Unable to get {fieldName} as it has not been initialized yet.");
   }
   
   /**
    * Init Plugin
    */
   private void Awake() {
      _LOGGER = base.Logger;

      _INSTANCE = this;
      
      base.Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} has begun setup...");

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

      // Required to convert image formats that are not supported to PNG
      MagickNET.Initialize();
      
      Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} loaded Successfully!");
   }
   
}