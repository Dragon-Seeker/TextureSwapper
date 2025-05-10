using System;
using System.Collections.Generic;
using io.wispforest.textureswapper.utils;
using Unity.VisualScripting;
using UnityEngine;

namespace io.wispforest.textureswapper.api.components.holders;

public class SwapperHandlerHolder : MonoBehaviour {

    private SwapperInstance<GeneralSwapper> _generalSwapper = new ();
    
    private Dictionary<MeshRenderer, Dictionary<int, MeshSwapper>> _meshToBaseHandlers = new ();
    private Dictionary<MeshRenderer, Dictionary<int, SwapperInstance<MeshSwapper>>> _meshToCurrentHandlers = new ();

    private Dictionary<int, MeshSwapper> getBaseHandlers(MeshRenderer mesh) {
        return _meshToBaseHandlers.computeIfAbsent(mesh, k => new Dictionary<int, MeshSwapper>()); 
    }
    
    private Dictionary<int, SwapperInstance<MeshSwapper>> getCurrentHandlers(MeshRenderer mesh) {
        return _meshToCurrentHandlers.computeIfAbsent(mesh, k => new Dictionary<int, SwapperInstance<MeshSwapper>>()); 
    }

    public SwapperHandlerHolder setCurrentGeneralHandlerAndSwap(GameObject gameObject, GeneralSwapper? handler = null) {
        _generalSwapper.reset(swapper => swapper.onUnswap(gameObject));

        if (handler is not null) {
            _generalSwapper = new SwapperInstance<GeneralSwapper>(handler, (generalSwapper, list) => generalSwapper.onSwap(gameObject, list));
        }
        
        return this;
    }

    public void setCurrentMeshHandlerAndSwap(GameObject gameObject, MeshRenderer mesh, int i, UnityEngine.Material prevMaterial, Action<UnityEngine.Material> materialSet, MeshSwapper? handler = null) {
        unswapIfPrevMeshInstance(gameObject, mesh, i);
        
        if (handler is null) {
            swapToBaseMeshInstance(gameObject, mesh, i, materialSet);
        } else {
            getBaseHandlers(mesh).computeIfAbsent(i, _ => new MaterialSwapper(Identifier.of("texture_swapper", "base_material"), prevMaterial));
            
            getCurrentHandlers(mesh)[i] = new SwapperInstance<MeshSwapper>(handler, (meshHandler, list) => {
                meshHandler.onSwap(gameObject, mesh, materialSet, list);
            });
        }
    }

    private void swapToBaseMeshInstance(GameObject gameObject, MeshRenderer mesh, int i, Action<UnityEngine.Material> materialSet) {
        var baseSwapper = getBaseHandlers(mesh).removeIfPresent(i);
            
        baseSwapper?.onSwap(gameObject, mesh, materialSet, new List<Component>());
    }

    private void unswapIfPrevMeshInstance(GameObject gameObject, MeshRenderer mesh, int i) {
        var currentHandlers = getCurrentHandlers(mesh);
        
        if (currentHandlers.ContainsKey(i)) {
            var previousInstance = currentHandlers.removeIfPresent(i);

            if (previousInstance is not null) {
                previousInstance.reset(handler => handler.onUnswap(gameObject, mesh));
            }
        }
    }

    public void unswapAllTextures(GameObject gameObject) {
        foreach (var meshToHandlers in _meshToCurrentHandlers) {
            var mesh = meshToHandlers.Key;
            
            UnityEngine.Material[] sharedMaterials = mesh.sharedMaterials;

            if (sharedMaterials == null) continue;

            var keys = new List<int>(meshToHandlers.Value.Keys);
            
            foreach (var i in keys) {
                unswapIfPrevMeshInstance(gameObject, mesh, i);
                
                if (i >= sharedMaterials.Length) continue;
                
                swapToBaseMeshInstance(gameObject, mesh, i, (newMaterial) => {
                    sharedMaterials[i] = newMaterial;

                    // Applying custom materials
                    mesh.sharedMaterials = sharedMaterials;
                });
            }
        }
        
        _meshToCurrentHandlers.Clear();
        _meshToBaseHandlers.Clear();
        _generalSwapper.reset(swapper => swapper.onUnswap(gameObject));
    }
}

public static class GameObjectExtension {
    public static void setSwapperHolder(this GameObject gameObject, MeshRenderer mesh, int i, UnityEngine.Material material, Action<UnityEngine.Material>setAction, MeshSwapper? handler) {
        if (gameObject is null) return;
        
        gameObject.GetOrAddComponent<SwapperHandlerHolder>().setCurrentMeshHandlerAndSwap(gameObject, mesh, i, material, setAction, handler);
    }
    
    public static void tryToAdjustMaterial(this GameObject gameObject, System.Action<MeshRenderer, int, UnityEngine.Material, Action<UnityEngine.Material>> onMatchEntry) { 
        SwapperComponentSetupUtils.searchMaterial(gameObject, (mesh, i, material, setAction) => {
            onMatchEntry(mesh, i, material, setAction);

            return false;
        });
    }
}

internal class SwapperInstance<S> where S : SwapperBase {
    public S? swapper { get; set; }
    public List<Component> attachedComponents { get; } = [];

    public SwapperInstance() { }
    
    public SwapperInstance(S swapper, Action<S, List<Component>> action) {
        this.swapper = swapper;

        action(swapper, attachedComponents);
    }

    public void reset(Action<S> prevSwapper) {
        if (swapper is not null) {
            prevSwapper(swapper);
            swapper = default;
        }

        foreach (var component in attachedComponents) {
            if (component.IsDestroyed()) continue;
            
            UnityEngine.Object.Destroy(component);
        }
        
        attachedComponents.Clear();
    }
}