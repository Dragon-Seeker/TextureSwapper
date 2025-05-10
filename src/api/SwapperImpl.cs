using System;
using System.Collections.Generic;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.utils;
using Unity.VisualScripting;
using UnityEngine;

namespace io.wispforest.textureswapper.api;

public interface SwapperBase {
    public Identifier id();

    public SwapperInteraction[] interactions();

    public bool allowMultiple() => true;
}

public interface GeneralSwapper : SwapperBase {
    public void onSwap(GameObject gameObject, List<Component> trackedComponents);

    public void onUnswap(GameObject gameObject) { }
}

public interface MeshSwapper : SwapperBase {
    public void onSwap(GameObject gameObject, MeshRenderer mesh, Action<UnityEngine.Material> materialSet, List<Component> trackedComponents);

    public void onUnswap(GameObject gameObject, MeshRenderer mesh) { }
}

public interface IsDelayed;

public enum SwapperInteraction {
    TEXTURE,
    AUDIO,
    OTHER
}

//--

public abstract class SwapperImpl(Identifier _id) : SwapperBase {

    private Identifier _id { get; } = _id;

    public Identifier id() {
        return _id;
    }

    public abstract SwapperInteraction[] interactions();

    public virtual bool allowMultiple() => true;

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        
        return obj.GetType() == GetType() && id().Equals((obj as SwapperImpl).id);
    }

    public override int GetHashCode() {
        return id().GetHashCode();
    }
}

public class EmptySwapper(Identifier id) : SwapperImpl(id), GeneralSwapper, MeshSwapper {
    private void onSwap() {
        Plugin.logIfDebugging(source => source.LogError($"Empty handler was swapped to a given object! [Name: {id}]"));
    }

    public void onSwap(GameObject gameObject, List<Component> trackedComponents) {
        onSwap();
    }

    public void onUnswap(GameObject gameObject) { }

    public void onSwap(GameObject gameObject, MeshRenderer mesh, Action<UnityEngine.Material> materialSet, List<Component> trackedComponents) {
        onSwap();
        
        MediaSwapperStorage.getHandler<MeshSwapper>(MediaIdentifiers.MISSING)!
                .onSwap(gameObject, mesh, materialSet, trackedComponents);
    }

    public void onUnswap(GameObject gameObject, MeshRenderer mesh) {
        MediaSwapperStorage.getHandler<MeshSwapper>(MediaIdentifiers.MISSING)!
                .onUnswap(gameObject, mesh);
    }

    public override SwapperInteraction[] interactions() => [SwapperInteraction.OTHER, SwapperInteraction.TEXTURE, SwapperInteraction.AUDIO];
}

public class DelayedMeshSwapper(Identifier id, bool multiple) : SwapperImpl(id), MeshSwapper, IsDelayed {
    
    public void onSwap(GameObject gameObject, MeshRenderer mesh, Action<UnityEngine.Material> materialSet, List<Component> trackedComponents) {
        MediaSwapperStorage.getHandler<MeshSwapper>(MediaIdentifiers.LOADING)!
                .onSwap(gameObject, mesh, materialSet, trackedComponents);
        
        MediaSwapperStorage.getOrActWithHandler<MeshSwapper>(id(), handler => handler.onSwap(gameObject, mesh, materialSet, trackedComponents));
    }

    public void onUnswap(GameObject gameObject, MeshRenderer mesh) {
        var handler = MediaSwapperStorage.getHandler(id());

        if (handler is MeshSwapper meshSwapperHandler) {
            meshSwapperHandler.onUnswap(gameObject, mesh);
        }
    }

    public override SwapperInteraction[] interactions() => [SwapperInteraction.TEXTURE];

    public override bool allowMultiple() => multiple;
}

public class DelayedGeneralSwapper(Identifier id, bool multiple) : SwapperImpl(id), GeneralSwapper, IsDelayed {
    
    public void onSwap(GameObject gameObject, List<Component> trackedComponents) {
        MediaSwapperStorage.getOrActWithHandler<GeneralSwapper>(id(), handler => handler.onSwap(gameObject, trackedComponents));
    }

    public void onUnswap(GameObject gameObject) {
        var handler = MediaSwapperStorage.getHandler(id());

        if (handler is GeneralSwapper generalSwapper) {
            generalSwapper.onUnswap(gameObject);
        }
    }

    public override SwapperInteraction[] interactions() => [];

    public override bool allowMultiple() => multiple;
}

//--

public class AudioSwapper(Identifier id, AudioClip clip) : SwapperImpl(id), GeneralSwapper {
    
    public void onSwap(GameObject gameObject, List<Component> trackedComponents) {
        var source = gameObject.GetOrAddComponent<AudioSource>();

        source.clip = clip;
        
        source.enabled = true;
        source.loop = true;
        source.volume = 1.0f;

        //source.spatialize = true;
        source.spatialBlend = 1;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0;
        source.minDistance = Plugin.ConfigAccess.minAudioDistance();
        source.maxDistance = Plugin.ConfigAccess.maxAudioDistance();
        
        source.playOnAwake = true;
        
        trackedComponents.Add(source);
        
        Plugin.Logger.LogWarning("Setting object with audio");
    }

    public override SwapperInteraction[] interactions() => [SwapperInteraction.AUDIO];
}

// TODO: FIGURE WAY OF REMOVING STUFF ON CLOSE OF THE GAME?
public class VideoSwapper(Identifier id, string filePath, MediaInfo mediaInfo, bool allowMultiple) : SwapperImpl(id), MeshSwapper {
    
    // TODO: FIND PROPER FIX FOR ALLOWING MULTIPLE VIDEOS!
    private bool _allowMultiple { get; } = false;
    
    public void onSwap(GameObject gameObject, MeshRenderer mesh, Action<UnityEngine.Material> materialSet, List<Component> trackedComponents) {
        Plugin.Logger.LogWarning($"Player being made: {filePath}!");
        
        MediaSwapperStorage.getHandler<MeshSwapper>(MediaIdentifiers.LOADING)!
                .onSwap(gameObject, mesh, materialSet, trackedComponents);
        
        VideoPlayerHolder.getOrCreate().createPlayer(this, filePath, mediaInfo, gameObject, materialSet);
        
    }
    
    public void onUnswap(GameObject gameObject, MeshRenderer mesh) {
        VideoPlayerHolder.getOrCreate().unswapObj(this, filePath, mediaInfo, gameObject);
    }

    public override SwapperInteraction[] interactions() => [SwapperInteraction.TEXTURE, SwapperInteraction.AUDIO];
    
    public override bool allowMultiple() => _allowMultiple;
}

public class MaterialSwapper(Identifier id, UnityEngine.Material material) : SwapperImpl(id), MeshSwapper {
    private UnityEngine.Material _material { get; } = material;

    public void onSwap(GameObject gameObject, MeshRenderer mesh, Action<UnityEngine.Material> materialSet, List<Component> trackedComponents) {
        materialSet(_material);
    }

    public override SwapperInteraction[] interactions() => [SwapperInteraction.TEXTURE];
}

public class MaterialListSwapper(Identifier id, UnityEngine.Material initialMaterial, Func<PNGImageSequence> sequenceConstructor) : SwapperImpl(id), MeshSwapper {

    private UnityEngine.Material _initialMaterial { get; } = initialMaterial;

    private Func<PNGImageSequence> _sequenceConstructor { get; } = sequenceConstructor;

    public void onSwap(GameObject gameObject, MeshRenderer mesh, Action<UnityEngine.Material> materialSet, List<Component> trackedComponents) {
        materialSet(_initialMaterial);

        ImageSequenceHolder.getOrCreate().handleSwap(this, materialSet, _sequenceConstructor);
    }

    public void onUnswap(GameObject gameObject, MeshRenderer mesh) {
        ImageSequenceHolder.getOrCreate().handleUnswap(this);
    }

    public override SwapperInteraction[] interactions() => [SwapperInteraction.TEXTURE];
}