using System;
using System.Collections.Generic;
using io.wispforest.textureswapper.utils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace io.wispforest.textureswapper.api.components.holders;

public class ActiveSwapperHolder : MonoEvent {
    private static GameObject? holderObj = null;

    private readonly Dictionary<Identifier, int> _activeSwapperCount = new ();

    private List<Identifier>? _meshSwapperEntries = null;
    private bool _meshListIsEmpty = false;
    private int _meshCutoffAmount = 1;
    
    private List<Identifier>? _generalSwapperEntries = null;
    private bool _generalListIsEmpty = false;
    private int _generalCutoffAmount = 1;
    
    public static ActiveSwapperHolder getOrCreate() {
        return HolderUtils.getOrCreate<ActiveSwapperHolder>(ref holderObj, () => holderObj = null, "ActiveSwapperHolder");
    }
    
    internal void reset() {
        _meshSwapperEntries = null;
        _meshListIsEmpty = false;
        _meshCutoffAmount = 1;
        
        _generalSwapperEntries = null;
        _generalListIsEmpty = false;
        _generalCutoffAmount = 1;
        
        _activeSwapperCount.Clear();
    }
    
    private List<Identifier> getMaterials<S>() where S : SwapperBase {
        var type = typeof(S);
        
        if (typeof(MeshSwapper).IsAssignableFrom(type)) {
            if (_meshSwapperEntries is null) {
                _meshSwapperEntries = getIdentifiers(_meshCutoffAmount, ref _meshListIsEmpty, MediaType.IMAGE, MediaType.VIDEO);
            }

            return _meshSwapperEntries;
        } 
        
        if (typeof(GeneralSwapper).IsAssignableFrom(type)) {
            if (_generalSwapperEntries is null) {
                _generalSwapperEntries = getIdentifiers(_generalCutoffAmount, ref _generalListIsEmpty, MediaType.UNKNOWN, MediaType.AUDIO);
            }

            return _generalSwapperEntries;
        } 
        
        throw new Exception($"Unable to get material for the given subtype: {typeof(S)}");
        
    }

    private bool materialsIsEmpty<S>() where S : SwapperBase {
        var type = typeof(S);
        
        if (typeof(MeshSwapper).IsAssignableFrom(type)) {
            return _meshListIsEmpty;
        } 
        
        if (typeof(GeneralSwapper).IsAssignableFrom(type)) {
            return _generalListIsEmpty;
        } 
        
        throw new Exception($"Unable to get material for the given subtype: {typeof(S)}");
    }

    private List<Identifier> getIdentifiers(int cutoffAmount, ref bool materialsIsEmpty, params MediaType[] types) {
        if (materialsIsEmpty) return [];
        
        var materials = MediaSwapperStorage.getMaterials(types, id => {
            var activeHandlersAmt = _activeSwapperCount.GetValueOrDefault(id, 0);

            if (Plugin.ConfigAccess.prioritizeNewPictures() && activeHandlersAmt > cutoffAmount) return false;
            
            var handler = MediaSwapperStorage.getHandler(id);
            
            return handler is null || handler.allowMultiple() || activeHandlersAmt <= 0;
        });

        if (materials.Count <= 0) {
            materialsIsEmpty = true;
        }

        return materials;
    }

    public Identifier? actOrWaitWithHandler<S>(Action<S> action) where S : SwapperBase {
        return actOrWaitWithHandler<S>(action, null);
    }

    public Identifier? actOrWaitWithHandler<S>(Action<S> action, int? hash) where S : SwapperBase {
        Random.State? prevState = null;

        var materials = getMaterials<S>();

        if (materials.Count <= 0) return null;

        if (hash is not null) {
            prevState = Random.state;
            Random.InitState((int)hash);
        }
        
        var index = Random.Range(0, materials.Count);
        
        if (prevState is not null) {
            Random.state = (Random.State) prevState;
        }

        var id = materials[index];
        
        var count = _activeSwapperCount.GetValueOrDefault(id, 0);
        
        _activeSwapperCount[id] = count + 1;

        if (!materialsIsEmpty<S>()) {
            var handler = MediaSwapperStorage.getHandler(id);
            
            if ((handler is not null && !handler.allowMultiple()) || Plugin.ConfigAccess.prioritizeNewPictures()) {
                var type = typeof(S);
                
                if (typeof(MeshSwapper).IsAssignableFrom(type)) {
                    _meshSwapperEntries!.Remove(id);

                    if (_meshSwapperEntries.Count <= 0) {
                        _meshCutoffAmount += 1;
                        
                        _meshSwapperEntries.AddRange(getIdentifiers(_meshCutoffAmount, ref _meshListIsEmpty, MediaType.IMAGE, MediaType.VIDEO));
                    }
                } else if (typeof(GeneralSwapper).IsAssignableFrom(type)) {
                    _generalSwapperEntries!.Remove(id);

                    if (_generalSwapperEntries.Count <= 0) {
                        _generalCutoffAmount += 1;
                        
                        _generalSwapperEntries.AddRange(getIdentifiers(_generalCutoffAmount, ref _generalListIsEmpty, MediaType.UNKNOWN, MediaType.AUDIO));
                    }
                } else {
                    throw new Exception($"Unable to reset materials for the given subtype: {typeof(S)}");
                }
            }
        }

        MediaSwapperStorage.getOrActWithHandler(id, action);
        
        return id;
    }
}