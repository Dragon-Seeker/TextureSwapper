using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using io.wispforest.textureswapper.utils;
using System.Linq;
using System.Reflection;
using BepInEx;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.api.config;

public class LayeredConfigFile(BepInPlugin ownerMetadata) : DummyConfigFile("layered_settings", ownerMetadata), IDictionary<ConfigDefinition, ConfigEntryBase> {
    
    protected readonly List<ConfigDefinition> defs = [];
    
    protected readonly SortedList<ConfigDefinition, ConfigEntryBase> entries;
    
    private readonly List<ConfigFile> files = [];

    public LayeredConfigFile(BaseUnityPlugin plugin, bool setAsConfigForPlugin = true) : this(MetadataHelper.GetMetadata(plugin)) {
        if (setAsConfigForPlugin) plugin.setPluginConfig(this);
        entries = new(new ConfigDefinitionComparer(indexGetter: getIndex));
    }

    private int getIndex(ConfigDefinition def) => defs.IndexOf(def);

    public ConfigEntry<T> Bind<T>(ConfigFile fileParent, string section, string key, T defaultValue, ConfigDescription? configDescription = null) {
        return Bind<T>(fileParent, new ConfigDefinition(section, key), defaultValue, configDescription);
    }

    public ConfigEntry<T> Bind<T>(ConfigFile fileParent, string section, string key, T defaultValue, string description) {
        return Bind<T>(fileParent, new ConfigDefinition(section, key), defaultValue, new ConfigDescription(description, null));
    }
    
    public ConfigEntry<T> Bind<T>(ConfigFile fileParent, ConfigDefinition def, T defaultValue, ConfigDescription? desc = null) {
        var entry = fileParent.Bind(def, defaultValue, desc);
        
        entries[def] = entry;
        defs.Add(def);
        
        if(!files.Contains(fileParent)) files.Add(fileParent);

        return entry;
    }
    
    public SectionBinder section(ConfigFile fileParent, string section) => new (this, fileParent, section);

    public new IEnumerator<KeyValuePair<ConfigDefinition, ConfigEntryBase>> GetEnumerator() => entries.GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    public bool Contains(KeyValuePair<ConfigDefinition, ConfigEntryBase> item) => entries.Contains(item);

    public int Count  => entries.Count;

    public bool ContainsKey(ConfigDefinition key) => entries.ContainsKey(key);

    public bool Remove(ConfigDefinition key) {
        files.Any(file => file.Remove(key));
        return entries.Remove(key);
    }

    public void Clear() {
        files.ForEach(file => file.Clear());
        entries.Clear();
    }
    
    ConfigEntryBase IDictionary<ConfigDefinition, ConfigEntryBase>.this[ConfigDefinition key] {
        get => this[key];
        set => throw new InvalidOperationException("Directly setting a config entry is not supported");
    }

    public new ConfigEntryBase this[ConfigDefinition key] => entries[key];

    public ICollection<ConfigDefinition> Keys => entries.Keys;
}

public static class CursedPluginExtensions {
    public static void setPluginConfig(this BaseUnityPlugin plugin, ConfigFile file) {
        typeof(BaseUnityPlugin)
            .GetRuntimeFields()
            .First(info => info.Name.Contains("Config"))
            ?.SetValue(plugin, file);
    }
}

public class ConfigDefinitionComparer(string cultureName = "en-US", CompareOptions? options = null, Func<ConfigDefinition, int>? indexGetter = null) : IComparer<ConfigDefinition> {

    private readonly CultureInfo info = createInfo(cultureName);
    
    private static CultureInfo createInfo(string cultureName) {
        try {
            return CultureInfo.CreateSpecificCulture(cultureName);
        } catch (Exception _) {
            return CultureInfo.CurrentCulture;
        }
    }
    
    public int Compare(ConfigDefinition? x, ConfigDefinition? y) {
        return baseCompare(x, y) * -1; 
    }

    private int baseCompare(ConfigDefinition? x, ConfigDefinition? y) {
        if (ReferenceEquals(x, y)) return 0;
        if (y is null) return 1;
        if (x is null) return -1;

        var value = info.CompareInfo.Compare(x.Section, y.Section, options ?? CompareOptions.None);

        if (value != 0) return value;

        return indexGetter?.Invoke(x).CompareTo(indexGetter(y)) ?? info.CompareInfo.Compare(x.Key, y.Key, options ?? CompareOptions.None);
    }
}