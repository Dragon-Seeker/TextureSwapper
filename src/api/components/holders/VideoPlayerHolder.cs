using System;
using System.Collections.Generic;
using io.wispforest.textureswapper.utils;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Video;

namespace io.wispforest.textureswapper.api.components.holders;

public class VideoPlayerHolder : MonoEvent {

    private Dictionary<VideoSwapper, VideoPlayer> activePlayers = new ();
    private Dictionary<VideoSwapper, List<Action<Material>>> activeMaterials = new ();
    private Dictionary<VideoSwapper, List<GameObject>> activeObjects = new ();
    private Dictionary<VideoSwapper, (GameObject, AudioSource?)> activeSource = new ();

    private static GameObject? holderObj = null;
    
    public static VideoPlayerHolder getOrCreate() {
        return HolderUtils.getOrCreate<VideoPlayerHolder>(ref holderObj, () => holderObj = null, "VideoPlayerHolder");
    }
    
    public void createPlayer(VideoSwapper handler, string filePath, MediaInfo mediaInfo, GameObject audioTargetObj, Action<Material>? currentAction = null) {
        if (activePlayers.ContainsKey(handler)) {
            var oldPlayer = activePlayers[handler];
            
            oldPlayer.targetTexture.Release();
            
            DestroyImmediate(oldPlayer);
            
            activePlayers.Remove(handler);
        }

        (GameObject, AudioSource?) targetData = activeSource.ContainsKey(handler) 
                ? activeSource[handler] 
                : addAudioSource(handler, mediaInfo, audioTargetObj);
        
        var player = targetData.Item1.GetOrAddComponent<VideoPlayer>();
        
        player.isLooping = true;
        player.source = VideoSource.Url;
        player.url = filePath;
        
        player.playOnAwake = false;

        if (mediaInfo.hasAudio) {
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.SetTargetAudioSource(0, targetData.Item2);
        } else {
            player.audioOutputMode = VideoAudioOutputMode.None;
        }

        activePlayers[handler] = player;

        var actions = activeMaterials.computeIfAbsent(handler, _ => []);
        
        if(currentAction is not null) actions.Add(currentAction);
        
        var texture = createTextureData(player, mediaInfo);
        
        player.prepareCompleted += _ => {
            player.Play();

            //var material = new Material(Shader.Find("Standard")) { mainTexture = texture };
            
            foreach (var action in activeMaterials.computeIfAbsent(handler, _ => [])) {
                action(new Material(Shader.Find("Standard")) { mainTexture = texture });
            }
        };

        var component = audioTargetObj.GetOrAddComponent<MonoEvent>();
        
        component.onStartCallback += o => {
            if (!player.IsDestroyed()) {
                player.Prepare();
            }
        };

        var objList = activeObjects.computeIfAbsent(handler, _ => []);
        
        objList.Add(audioTargetObj);

        component.onDestoryCallback += o => {
            unswapObj(handler, filePath, mediaInfo, o);
        };
    }

    public void unswapObj(VideoSwapper handler, string filePath, MediaInfo mediaInfo, GameObject obj) {
        var objList = activeObjects.computeIfAbsent(handler, _ => []);
        
        objList.Remove(obj);
            
        var data = activeSource[handler];
            
        if (data.Item1.Equals(obj)) {
            activeSource.Remove(handler);
        }

        foreach (var gameObject1 in objList) {
            createPlayer(handler, filePath, mediaInfo, gameObject1);
                
            break;
        }
    }
    
    public (GameObject, AudioSource?) addAudioSource(VideoSwapper handler, MediaInfo mediaInfo, GameObject audioTargetObj) {
        AudioSource? source = null;
        
        if (mediaInfo.hasAudio) { 
            source = audioTargetObj.AddComponent<AudioSource>();
        
            source.enabled = true;
            source.loop = true;
            source.volume = 1.0f;

            //source.spatialize = true;
            source.spatialBlend = 1;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0;
            source.minDistance = Plugin.ConfigAccess.minAudioDistance();
            source.maxDistance = Plugin.ConfigAccess.maxAudioDistance();
        }

        var pair = (audioTargetObj, source);
        
        activeSource[handler] = (audioTargetObj, source);

        return pair;
    }
    
    public RenderTexture createTextureData(VideoPlayer source, MediaInfo info) {
        var renderTexture = new RenderTexture(info.width, info.height, 0, RenderTextureFormat.ARGB32);
        renderTexture.antiAliasing = 1;
        renderTexture.useMipMap = false;
        //renderTexture.SetSRGBReadWrite(true);
        renderTexture.Create();
        
        source.targetTexture = renderTexture;

        return renderTexture;
    }
}