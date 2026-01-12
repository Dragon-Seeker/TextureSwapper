using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using io.wispforest.textureswapper.utils;
using System.Linq;
using System.Reflection;
using BepInEx;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.api.config;

public class LayeredConfigFile : DummyConfigFile, IDictionary<ConfigDefinition, ConfigEntryBase> {
    protected readonly IList<ConfigFile> configFileOrder = [];

    public LayeredConfigFile(BaseUnityPlugin plugin, bool setAsConfigForPlugin = true) : this(MetadataHelper.GetMetadata(plugin)) {
        if (setAsConfigForPlugin) plugin.setPluginConfig(this);
    }

    public LayeredConfigFile(BepInPlugin ownerMetadata) : base("layered_settings", ownerMetadata) {
        this.SaveOnConfigSet = false;
    }
    
    public new IEnumerator<KeyValuePair<ConfigDefinition, ConfigEntryBase>> GetEnumerator() => configFileOrder.SelectMany((t) => EnumerableUtils.delegating(t.GetEnumerator)).GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    public bool Contains(KeyValuePair<ConfigDefinition, ConfigEntryBase> item) => configFileOrder.Any(file => file.Contains(item));

    public int Count => configFileOrder.Sum(file => file.Count);

    public bool ContainsKey(ConfigDefinition key) => configFileOrder.Any(file => file.ContainsKey(key));

    public bool Remove(ConfigDefinition key) => configFileOrder.Any(file => file.Remove(key));

    public void Clear() => configFileOrder.ForEach(file => file.Clear());
    
    ConfigEntryBase IDictionary<ConfigDefinition, ConfigEntryBase>.this[ConfigDefinition key] {
        get => this[key];
        set => throw new InvalidOperationException("Directly setting a config entry is not supported");
    }

    public new ConfigEntryBase this[ConfigDefinition key] {
        get {
            foreach (var configFile in configFileOrder) {
                if (configFile.ContainsKey(key)) return configFile[key];
            }

            return null;
        }
    }

    public ICollection<ConfigDefinition> Keys => configFileOrder.SelectMany(file => file.Keys).ToList();
}

public static class CursedPluginExtensions {
    public static void setPluginConfig(this BaseUnityPlugin plugin, ConfigFile file) {
        typeof(BaseUnityPlugin)
            .GetRuntimeFields()
            .First(info => info.Name.Contains("Config"))
            ?.SetValue(plugin, file);
    }
}