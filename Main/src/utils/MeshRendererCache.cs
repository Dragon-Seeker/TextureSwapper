using System.Collections.Generic;
using System.Collections.ObjectModel;
using io.wispforest.textureswapper.api.components;
using io.wispforest.textureswapper.api.components.holders;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace io.wispforest.textureswapper.utils;

public class MeshRendererCache : MonoEvent {
    
    private static GameObject? holderObj = null;

    private bool addedAllMeshes = false;
    private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    
    public static MeshRendererCache getOrCreate() {
        return HolderUtils.getOrCreate<MeshRendererCache>(ref holderObj, () => holderObj = null, "MeshRendererCache");
    }

    public List<MeshRenderer> getRenderers(bool refreshRenderers = false, bool clearOldRenderers = false) {
        if (!addedAllMeshes || refreshRenderers) {
            renderers.Clear();
            addedAllMeshes = true;
            renderers.AddRange(FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None));
        }

        if (clearOldRenderers) checkRenderers();

        return renderers!;
    }

    // TODO: HOOK INTO KNOWN OBJECT INSTANTIATION 
    public void addRenderer(MeshRenderer renderer) {
        getRenderers().Add(renderer);
    }
    
    public void addRenderers(ICollection<MeshRenderer> renderers) {
        getRenderers().AddRange(renderers);
    }

    public void checkRenderers() { ;
        getRenderers().RemoveAll(renderer => renderer.IsDestroyed());
    }
}