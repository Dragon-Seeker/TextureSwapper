using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using io.wispforest.textureswapper.utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace io.wispforest.textureswapper.api.components.holders;

public class ImageSequenceHolder : MonoEvent {

    private readonly Dictionary<MaterialListSwapper, int> _handlerToActiveAmount = new ();
    private readonly Dictionary<MaterialListSwapper, IList<Action<UnityEngine.Material>>> _waitingSwapperActions = new();
    private readonly Dictionary<MaterialListSwapper, (UnityEngine.Material, TextureSequence, UpdatableInstanceComponent)> _currentlyLoaderSequences = new();
    private readonly HashSet<MaterialListSwapper> _loadingSequences = new ();
    
    private static GameObject? _HOLDER_OBJ = null;
    
    public static ImageSequenceHolder getOrCreate() {
        return HolderUtils.getOrCreate<ImageSequenceHolder>(ref _HOLDER_OBJ, () => _HOLDER_OBJ = null, "ImageSequenceHolder");
    }
    
    public static void actIfPresent(Action<ImageSequenceHolder> action) {
        if (_HOLDER_OBJ is not null) {
            action(getOrCreate());
        }
    }

    public ImageSequenceHolder() {
        this.onDestoryCallback += _ => {
            foreach (var sequenceData in _currentlyLoaderSequences.Values) {
                Object.Destroy(sequenceData.Item3);
                sequenceData.Item2.invalidateTextures();
                MaterialUtils.invalidateMaterial(sequenceData.Item1);
            }
            
            _currentlyLoaderSequences.Clear();
        };

        this.onUpdateCallback += _ => {
            foreach (var valueTuple in _currentlyLoaderSequences.Values) {
                valueTuple.Item3.Update();
            }
        };
    }

    private void incrementAmount(MaterialListSwapper swapper) {
        var amount = _handlerToActiveAmount.computeIfAbsent(swapper, _ => 0) + 1;
            
        _handlerToActiveAmount[swapper] = amount;
    }
    
    private void decrementAmount(MaterialListSwapper swapper) {
        var amount = _handlerToActiveAmount.computeIfAbsent(swapper, _ => 0) - 1;
            
        _handlerToActiveAmount[swapper] = Math.Max(amount, 0);

        if (amount > 0) return;

        if (_currentlyLoaderSequences.Remove(swapper, out var sequenceData)) {
            Object.Destroy(sequenceData.Item3);
            MaterialUtils.invalidateMaterial(sequenceData.Item1);
            sequenceData.Item2.invalidateTextures();
        }
    }

    public void handleSwap(MaterialListSwapper swapper, Action<UnityEngine.Material> swapAction, Func<PNGImageSequence> sequenceMaker) {
        if (_currentlyLoaderSequences.TryGetValue(swapper, out var sequenceData)) {
            swapAction(sequenceData.Item1);

            incrementAmount(swapper);
        } else {
            _waitingSwapperActions.computeIfAbsent(swapper, _ => [])
                    .Add(swapAction);

            if (_loadingSequences.Contains(swapper)) return;
            
            MultiThreadHelper.run(MediaFormat.ENCODING_GROUP, () => {
                try {
                    var sequenceData = sequenceMaker();
                    
                    MainThreadHelper.runOnMainThread(() => {
                        var textureSequence = sequenceData.convertToMaterialSequence(swapper.id());

                        var material = MaterialUtils.createMaterial(null, swapper.id());

                        var animationHandler =
                                new UpdatableInstanceComponent().createAndSetInstance(sequenceData.maxAnimationTime, sequenceData.deltaBetweenFrames, i => {
                                    if (i >= textureSequence.imageSlices.Count) return true;
                                    
                                    material.mainTexture = textureSequence.imageSlices[i];

                                    return false;
                                });
                        
                        _currentlyLoaderSequences[swapper] = new (material, textureSequence, animationHandler);
                        _loadingSequences.Remove(swapper);
                    });
                } catch (Exception e) {
                    Plugin.logIfDebugging(source => {
                        source.LogError($"Unable load the needed materials for the required handler: {swapper.id}");
                        source.LogError(e);
                    });
                } 
            });

            _loadingSequences.Add(swapper);
        }
    }

    public void handleUnswap(MaterialListSwapper swapper) {
        decrementAmount(swapper);
    }

    internal void checkIfMaterialsLoaded() {
        var swappedHandlers = new HashSet<MaterialListSwapper>();
        
        foreach (var entry in _waitingSwapperActions) {
            var handler = entry.Key;
            
            if (!_currentlyLoaderSequences.ContainsKey(handler)) continue;
            
            swappedHandlers.Add(handler);

            var sequenceData = _currentlyLoaderSequences[handler];
            
            foreach (var action in entry.Value) {
                action(sequenceData.Item1);
                
                incrementAmount(handler);
            }
        }
        
        foreach (var materialListHandler in swappedHandlers) {
            _waitingSwapperActions.Remove(materialListHandler);
        }
    }
}