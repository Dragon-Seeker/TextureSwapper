using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using io.wispforest.textureswapper.api;
using io.wispforest.textureswapper.api.components;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.api.query.impl;
using Photon.Pun;
using Sirenix.Utilities;
using Unity.VisualScripting;
using UnityEngine;
using Material = UnityEngine.Material;
using Random = UnityEngine.Random;

namespace io.wispforest.textureswapper.utils;

public class SwapperComponentSetupUtils {
    public static void unswapScene(GameObject rootObject) {
        if (!Plugin.ConfigAccess.clientSideOnly()) return;
        
        foreach (var obj in unpackGameObject(rootObject)) {
            obj.GetComponent<SwapperHandlerHolder>()?.unswapAllTextures(obj);
        }
    }
    
    public static void commonSide(GameObject rootObject) {
        try {
            foreach (var obj in unpackGameObject(rootObject)) {
                System.Action<MeshRenderer, int, Material, Action<Material>> onMatchEntry = (mesh, i, material, setAction) => {
                    if (Plugin.ConfigAccess.clientSideOnly()) {
                        fullClientSideSetup(obj, mesh, i, material, setAction);
                        
                        return;
                    }
                    
                    var paintingComponent = obj.GetOrAddComponent<MediaIdentifierComponent>();

                    if (PhotonNetwork.LocalPlayer.IsMasterClient) {
                        var id = ActiveSwapperHolder.getOrCreate().actOrWaitWithHandler<MeshSwapper>(handler => {
                            obj.setSwapperHolder(mesh, i, material, setAction, handler);
                        });

                        // TODO: PROMPT THAT WE ARE EMPTY ONCE?
                        if (id is null) return;
                        
                        if (paintingComponent is not null) {
                            paintingComponent.setId(id);
                        } else {
                            Plugin.logIfDebugging(source => source.LogWarning($"Unable to create or get the needed Painting Component for given obj"));
                        }
                    }

                    var photonView = obj.GetOrAddComponent<PhotonView>();

                    Plugin.logIfDebugging(source => source.LogInfo($"Target Object preparing for Texture Component Replacment: {obj.name}"));

                    if (photonView == null) {
                        Plugin.logIfDebugging(source => source.LogInfo($"Target Object was unable to have a Photon View\n"));
                    } else {
                        Plugin.logIfDebugging(source => source.LogInfo($"Target Object View will now track Painting Component!\n"));

                        (obj.GetPhotonView().ObservedComponents ??= new()).Add(paintingComponent);
                    }
                    
                };
                
                obj.tryToAdjustMaterial(onMatchEntry);
            }
        } catch (Exception e) {
            Plugin.Logger.LogError("An error has occured when adjusting painting material!");
            Plugin.Logger.LogError(e); 
        }
    }

    public static void fullClientSideSetup(GameObject obj, MeshRenderer mesh, int i, Material material, Action<Material> setAction) {
        var pos = obj.transform.position;
        var hash = BitConverter.ToInt32(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes($"{pos.x:R},{pos.y:R},{pos.z:R}")), 0);
        
        ActiveSwapperHolder.getOrCreate().actOrWaitWithHandler<MeshSwapper>(handler => {
            obj.setSwapperHolder(mesh, i, material, setAction, handler);
        }, hash);
    }
    
    public static void clientPaintingDataChange(GameObject obj, FullMediaData fullData) {
        MediaSwapperStorage.loadIfNotFound(fullData);
        
        System.Action<MeshRenderer, int, Material, Action<Material>> onMatchEntry = (mesh, i, material, setAction) => {
            var id = fullData.id;

            if (MediaIdentifiers.ERROR.Equals(id) || !MediaSwapperStorage.hasMaterial(id)) {
                Plugin.logIfDebugging(source => source.LogInfo($"Unable to Set Clients Material: {id}"));
            }
            
            MediaSwapperStorage.getOrActWithHandler<MeshSwapper>(id, handler => {
                if (isCensored(MediaSwapperStorage.getResult(id))) {
                    handler = MediaSwapperStorage.getHandler<MeshSwapper>(MediaIdentifiers.CENSORED)!;
                    
                    Plugin.logIfDebugging(source => source.LogInfo($"The given entry {id} has been censored due to being blacklisted!"));
                }
                
                obj.gameObject.setSwapperHolder(mesh, i, material, setAction, handler);

                Plugin.logIfDebugging(source => source.LogInfo($"Set Clients Material: {id}"));
            });
        };
        
        obj.tryToAdjustMaterial(onMatchEntry);
    }
    
    public static bool isPictureGameObject(GameObject gameObject) {
        return searchMaterial(gameObject, (_, _, _, _) => true);
    }
    
    public static bool searchMaterial(GameObject gameObject, System.Func<MeshRenderer, int, Material, Action<Material>, bool> onMatchEntry) {
        var pictureTargets = Plugin.ConfigAccess.pictureTextureTargets.Select(s => RegexUtils.parseRegexWithFlags(s)).ToList();
        
        // Traversing all MeshRenderers of the object
        foreach (MeshRenderer mesh in gameObject.GetComponents<MeshRenderer>()) {
            // Storing the shared materials of the MeshRenderer
            Material[] sharedMaterials = mesh.sharedMaterials;

            if (sharedMaterials == null) continue;

            // Traversing all shared materials of the MeshRenderer
            for (int i = 0; i < sharedMaterials.Length; i++) {
                Material material = sharedMaterials[i];
                if (material != null) {
                    var name = material.name;

                    var match = pictureTargets.Any(regex => regex.IsMatch(name)) || pictureTargets.Any(regex => regex.IsMatch(mesh.gameObject.name)) ;
                    
                    if (match/*pictureTargets.Contains(name) || pictureTargets.Contains("*") || mesh.gameObject.name == "painting"*/) {
                        var targetIndex = i;
                        var setMaterialCallback = (Material newMaterial) => {
                            sharedMaterials[targetIndex] = newMaterial;

                            // Applying custom materials
                            mesh.sharedMaterials = sharedMaterials;
                        };
                        
                        if (onMatchEntry(mesh, targetIndex, material, setMaterialCallback)) return true;
                    }
                    
                }
            }
        }
        
        return false;
    }
    
    public static IEnumerable<GameObject> unpackGameObject(GameObject gameObject) {
        var children = gameObject.GetComponentsInChildren<Transform>().Select(child => child.gameObject);

        return new[] { gameObject }.Concat(children);
    }

    public static bool isCensored(MediaQueryResult queryResult) {
        bool censorEntry = false;
        
        if (Plugin.ConfigAccess.restrictiveQueries()) {
            if (queryResult is RatedMediaResult queryRating && !queryRating.getRating().Equals(MediaRating.SAFE)) {
                return true;
            }

            if (!censorEntry && queryResult is TaggedMediaResult taggedMediaResult) {
                foreach (var tag in Plugin.ConfigAccess.blackListTags) {
                    if (taggedMediaResult.hasTag(tag)) return true;
                }
            }
        }

        return false;
    }
}

