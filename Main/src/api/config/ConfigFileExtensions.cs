using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;

namespace io.wispforest.textureswapper.utils;

public static class ConfigFileExtensions {
    public static SectionBinder section(this ConfigFile file, string section) => new (file, section);
    
    public static void settingsChangedSafe<T>(this ConfigEntry<T> entry, EntryChangedHandler<T> changedHandler) {
        var isLocked = false;
        entry.SettingChanged += (sender, _) => {
            if (isLocked || !(sender is ConfigEntry<T> changedEntry)) return;

            isLocked = true;

            changedHandler(changedEntry, changedEntry.Value, t => changedEntry.Value = t);
            
            isLocked = false;
        };
    }
}

public class ConfigEntryBuilder<T, R>(SectionBinder binder, string key, Converter<T, R> converter) {

    private Converter<T, R> converter = converter;
    
    private R _defaultValue;
    private bool hasDefaultValue = false;
    
    private string? _description;
    private AcceptableValueBase? _acceptableValues;
    private object[]? _tags;

    internal EntryChangedHandler<R>? _onChangeCallback;
    
    private bool hasDescription = false;

    public ConfigEntryBuilder<T, R> defaultValue(R defaultValue) {
        _defaultValue = defaultValue;
        hasDefaultValue = true;

        return this;
    }
    
    public ConfigEntryBuilder<T, R> description(string? description) {
        _description = description;
        
        return this;
    }
    
    public ConfigEntryBuilder<T, R> description(AcceptableValueBase acceptableValues) {
        _acceptableValues = acceptableValues;
        
        return this;
    }
    
    public ConfigEntryBuilder<T, R> tags(object[]? tags) {
        _tags = tags;
        
        return this;
    }

    public ConfigEntryBuilder<T, R> onChange(EntryChangedHandler<R> callback) {
        this._onChangeCallback = callback;

        return this;
    }
    
    public ConfigEntryBuilder<T, R> onChange(Validator<R> validator) {
        return onChange((_, newValue, setter) => {
            var value = validator(newValue);
            
            setter(value);

            Plugin.logIfDebugging(() => $"{key} value has been updated! Value: [{value}]");
        });
    }
    
    public SectionBinder bind(out ConfigEntry<T> field) {
        var configDescription = hasDescription ? new ConfigDescription(_description ?? "", _acceptableValues, _tags ?? []) : null;
        
        binder.bind<T>(out field, key, hasDefaultValue ? converter.from(_defaultValue) : default, configDescription);

        if (_onChangeCallback != null) {
            field.settingsChangedSafe((entry, value, setter) => {
                _onChangeCallback(entry, converter.to(value), v => converter.to(setter(converter.from(v))));
            });
        }
        
        return binder;
    }
}

public delegate void Builder<T, R>(ConfigEntryBuilder<T, R> builder);

public delegate T Validator<T>(T t);

public delegate void EntryChangedHandler<T>(ConfigEntryBase entry, T newValue, Func<T, T> setter);

public delegate T Getter<out T>();

public interface Converter {
    public static Converter<T, R> of<T, R>(System.Converter<T, R> to, System.Converter<R, T> from) => new ConvertorImpl<T, R>(to, from);
    
    public static Converter<T, T> of<T>() => new ConvertorImpl<T, T>(t => t, t => t);

    public static readonly Converter<string, IList<string>> COMMA_SEPARATED_LIST = 
        of<string, IList<string>>(input => input.Split(",").ToList(), input => input.Join(delimiter: ","));
}

public interface Converter<T, R> : Converter {
    R to(T t);

    T from(R r);

    public Converter<T, A> xmap<A>(System.Converter<R, A> to, System.Converter<A, R> from) => new ConvertorImpl<T, A>(t => to(this.to(t)), a => this.from(from(a)));
}

public class ConvertorImpl<T, R>(System.Converter<T, R> _to, System.Converter<R, T> _from) : Converter<T, R> {
    public R to(T t) => _to(t);
    public T from(R r) => _from(r);
}

public class SectionBinder(ConfigFile configFile, string section) {
    
    public SectionBinder bind<T>(string key, T defaultValue, Builder<T, T>? builderAction) => bind(key, null, defaultValue, out ConfigEntry<T> _, builderAction);

    public SectionBinder bind<T>(string key, T defaultValue, out ConfigEntry<T> field, Builder<T, T>? builderAction) => bind(key, null, defaultValue, out field, builderAction);
    
    public SectionBinder bind<T>(string key, string description, T defaultValue, Builder<T, T>? builderAction = null) => bind(key, description, defaultValue, out ConfigEntry<T> _, builderAction);

    public SectionBinder bind<T>(string key, string? description, T defaultValue, out ConfigEntry<T> field, Builder<T, T>? builderAction = null) {
        var builder = new ConfigEntryBuilder<T, T>(this, key, Converter.of<T>());

        builder.description(description)
            .defaultValue(defaultValue);
        
        builderAction?.Invoke(builder);

        builder.bind(out field);

        return this;
    }
    
    //--

    public SectionBinder bind<T>(string key, string? description, T defaultValue, out Getter<T> getter, Builder<T, T>? builderAction = null) {
        bind(key, description, defaultValue, out ConfigEntry<T> entry, builderAction);

        getter = () => entry.Value;

        return this;
    }

    public SectionBinder bind<T, R>(string key, string? description, R defaultValue, Converter<T, R> converter, Builder<T, R>? builderAction = null) =>
        bind(key, description, defaultValue, out _, converter, builderAction);
    
    public SectionBinder bind<T, R>(string key, string? description, R defaultValue, out Getter<R> getter, Converter<T, R> converter, Builder<T, R>? builderAction = null) {
        var builder = new ConfigEntryBuilder<T, R>(this, key, converter);

        builder.description(description)
            .defaultValue(defaultValue);
        
        builderAction?.Invoke(builder);

        var originalCallback = builder._onChangeCallback;

        var hasChanged = true;
        
        builder._onChangeCallback = (entry, value, setter) => {
            originalCallback?.Invoke(entry, value, setter);

            hasChanged = true;
        };

        builder.bind(out var field);

        var value = converter.to(field.Value);
        
        getter = () => {
            if (hasChanged) value = converter.to(field.Value);

            return value;
        };

        return this;
    }
    
    //--
    
    internal SectionBinder bind<T>(out ConfigEntry<T> field, string key, T defaultValue, ConfigDescription? configDescription = null) {
        field = configFile.Bind(section, key, defaultValue, configDescription);
        
        return this;
    }
    
}