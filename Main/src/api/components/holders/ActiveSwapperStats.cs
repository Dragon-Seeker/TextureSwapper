using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.utils;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

namespace io.wispforest.textureswapper.api.components.holders;

// Class used to keep track of active swapper instances and the pools for base swapper states
public class ActiveSwapperStats : MonoEvent {
    private static GameObject? holderObj = null;
    
    public static ActiveSwapperStats getOrCreate() {
        var newHolder = holderObj is null;
        
        var holder = HolderUtils.getOrCreate<ActiveSwapperStats>(ref holderObj, () => holderObj = null, "ActiveSwapperHolder");

        if (newHolder && Plugin.config.prioritizeNewPicturesAcrossLevels()) {
            if (Plugin.prevHolder is not null) holder.importFrom(Plugin.prevHolder);
            
            Plugin.prevHolder = holder;
        }
        
        return holder;
    }

    // Map that holds the amount of active swappers of the given id exist currently
    private readonly Dictionary<Identifier, int> _activeSwapperInstances = new ();

    private readonly Dictionary<Type, Pool> swapperTypeToEntries = new();

    private Pool? getPool<T>() where T : SwapperBase => getPool(SwapperBase.getBaseType<T>());
    
    private Pool? getPool(Type? type) => type != null ? swapperTypeToEntries.computeIfAbsent(type, (t) => new Pool(t)) : null;
    
    internal void reset() {
        _activeSwapperInstances.Clear();
        
        foreach (var swapperEntriese in swapperTypeToEntries.Values) {
            swapperEntriese.reset();
        }
    }

    internal void importFrom(ActiveSwapperStats prevStats) {
        _activeSwapperInstances.Clear();
        
        _activeSwapperInstances.AddRange(prevStats._activeSwapperInstances);
        
        prevStats.swapperTypeToEntries.forEach((type, entries) => getPool(type)!.cutoff = entries.cutoff);
    }
    
    private List<Identifier> getIds<S>() where S : SwapperBase {
        return getPool<S>()?.getIds(getMaterialsFor) ?? throw new Exception($"Unable to get material for the given subtype: {typeof(S)}");
    }

    private bool foundNoNewIds<S>() where S : SwapperBase {
        return getPool<S>()?.foundNoNewIds ?? throw new Exception($"Unable to get material for the given subtype: {typeof(S)}");
    }

    private List<Identifier> getMaterialsFor(int cutoff, MediaType[] types) {
        return MediaSwapperStorage.getMaterials(types, id => {
            var instanceCount = _activeSwapperInstances.GetValueOrDefault(id, 0);

            if (Plugin.config.prioritizeNewPictures() && instanceCount >= cutoff) return false;

            var key = MediaSwapperStorage.getQueryKey(id);

            if (key is not null && UserSettings.isAlternativeCensorImage(key)) return false;

            var handler = MediaSwapperStorage.getHandler(id);

            return handler is null || handler.allowMultiple() || instanceCount <= 0;
        });
    }

    public Identifier? actOrWaitWithHandler<S>(Action<S> action, int? hash = null) where S : SwapperBase {
        var materials = getIds<S>();

        if (materials.Count <= 0) return null;
        
        var index = RandomUtils.preserveState(() => Random.Range(0, materials.Count), hash);

        var id = materials[index];
        
        _activeSwapperInstances[id] = _activeSwapperInstances.GetValueOrDefault(id, 0) + 1;

        if (!foundNoNewIds<S>()) {
            var handler = MediaSwapperStorage.getHandler(id);
            
            if ((handler is not null && !handler.allowMultiple()) || Plugin.config.prioritizeNewPictures()) {
                var entries = getPool<S>();

                if (entries == null) throw new Exception($"Unable to reset materials for the given subtype: {typeof(S)}");
                
                entries.removeId(id, getMaterialsFor);
            }
        }

        MediaSwapperStorage.getOrActWithHandler(id, action);
        
        return id;
    }
}

internal class Pool {
    // All Current ids that could be swapped to
    private List<Identifier>? currentIds;
    
    // A flag value to indicate that the given look was found to be empty even
    // after setting the values and used as a way to prevent constant relooking when nothing will be found 
    public bool foundNoNewIds; 
    
    // The current roll over iterated when every id in the pool has been chosen
    public int cutoff = 1;
    
    private readonly MediaType[] types = [];

    public Pool(Type t) {
        if (t == typeof(MeshSwapper)) {
            types = [MediaType.IMAGE, MediaType.VIDEO];
        } else if (t == typeof(GeneralSwapper)) {
            types = [MediaType.AUDIO, MediaType.UNKNOWN];
        }
    }

    public void reset() {
        currentIds = null;
        foundNoNewIds = false;
        cutoff = 1;
    }

    public void setIds(List<Identifier> value) {
        if (value.isEmpty()) {
            foundNoNewIds = true;
        } else {
            foundNoNewIds = false;
            currentIds = value;
        }
    }
    
    public List<Identifier> getIds(Func<int, MediaType[], List<Identifier>> getter) {
        // TODO: REWRITE AS NOT VERY PERFORMANT I BELIEVE
        if (!foundNoNewIds) setIds(getter(cutoff, types));

        return currentIds ?? [];
    }

    public void removeId(Identifier id, Func<int, MediaType[], List<Identifier>> getter) {
        currentIds!.Remove(id);
        
        if (currentIds!.Count > 0) return;
        
        cutoff += 1;
        
        setIds(getter(cutoff, types));
    }
}