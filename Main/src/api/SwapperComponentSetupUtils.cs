using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using io.wispforest.textureswapper.api.components;
using io.wispforest.textureswapper.api.components.holders;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.api.query.impl;
using io.wispforest.textureswapper.api.target;
using io.wispforest.textureswapper.utils;
using Photon.Pun;
using Unity.VisualScripting;
using UnityEngine;
using Material = UnityEngine.Material;

namespace io.wispforest.textureswapper.api;

public class SwapperComponentSetupUtils {
    public static void unswapScene(GameObject rootObject) {
        if (!Plugin.config.clientSideMode()) return;
        
        foreach (var obj in rootObject.unpackGameObject()) {
            obj.GetComponent<SwapperHandlerHolder>()?.unswapAllTextures(obj);
        }
    }
    
    public static void commonSide(GameObject rootObject, bool clientSideOverride = false) {
        var level = RunManager.instance.levelCurrent;
        
        try {
            foreach (var obj in rootObject.unpackGameObject()) {
                tryToAdjustMaterial(level, obj, (queryIds, mesh, i, material, setAction) => {
                    if (Plugin.config.clientSideMode() || clientSideOverride) {
                        fullClientSideSetup(obj, mesh, i, material, setAction);
                        
                        return;
                    }
                    
                    // TODO: IDK IF WE CAN HANDLE MORE THAN ONE SWAP PER OBJ TARGET....
                    var paintingComponent = obj.GetOrAddComponent<MediaIdentifierComponent>();

                    if (PhotonNetwork.LocalPlayer.IsMasterClient) {
                        var id = ActiveSwapperStats.getOrCreate().actOrWaitWithHandler<MeshSwapper>(handler => {
                            var id = handler.id();
                            
                            if (isCensored(MediaSwapperStorage.getResult(id))) {
                                handler = MediaSwapperStorage.getHandler<MeshSwapper>(UserSettings.getAlternativeCensorImage() ?? MediaIdentifiers.CENSORED)!;
                    
                                Plugin.logIfDebugging(source => source.LogInfo($"The given entry {id} has been censored due to being blacklisted!"));
                            } 
                            
                            obj.setSwapperHolder(mesh, i, material, setAction, handler);
                        });

                        // TODO: PROMPT THAT WE ARE EMPTY ONCE?
                        if (id is null) {
                            Plugin.Logger.LogWarning($"Unable to swap object as the found id was null! [Obj: {rootObject.name}]");
                            return;
                        }
                        
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

                        (obj.GetPhotonView().ObservedComponents ??= []).Add(paintingComponent);
                    }
                    
                });
            }
        } catch (Exception e) {
            Plugin.Logger.LogError("An error has occured when adjusting painting material!");
            Plugin.Logger.LogError(e); 
        }
    }

    public static void fullClientSideSetup(GameObject obj, MeshRenderer mesh, int i, Material material, Action<Material> setAction) {
        var pos = obj.transform.position;
        var hash = BitConverter.ToInt32(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes($"{Math.Floor(pos.x)},{Math.Floor(pos.y)},{Math.Floor(pos.z)},{obj.name}")), 0);
        
        var id = ActiveSwapperStats.getOrCreate().actOrWaitWithHandler<MeshSwapper>(handler => {
            var id = handler.id();
            
            if (isCensored(MediaSwapperStorage.getResult(id))) {
                handler = MediaSwapperStorage.getHandler<MeshSwapper>(UserSettings.getAlternativeCensorImage() ?? MediaIdentifiers.CENSORED)!;
                    
                Plugin.logIfDebugging(source => source.LogInfo($"The given entry {id} has been censored due to being blacklisted!"));
            } 
            
            obj.setSwapperHolder(mesh, i, material, setAction, handler);
        }, hash);
        
        var paintingComponent = obj.GetOrAddComponent<MediaIdentifierComponent>();
        
        if (id != null) paintingComponent.setId(id);
    }
    
    public static void clientPaintingDataChange(GameObject obj, FullMediaData fullData) {
        MediaSwapperStorage.loadIfNotFound(fullData);
        
        var level = RunManager.instance.levelCurrent;
        
        tryToAdjustMaterial(level, obj, (queryIds, mesh, i, material, setAction) => {
            var id = fullData.id;

            if (MediaIdentifiers.ERROR.Equals(id) || !MediaSwapperStorage.hasMaterial(id)) {
                Plugin.logIfDebugging(source => source.LogInfo($"Unable to Set Clients Material: {id}"));
            }
            
            MediaSwapperStorage.getOrActWithHandler<MeshSwapper>(id, handler => {
                if (isCensored(MediaSwapperStorage.getResult(id))) {
                    handler = MediaSwapperStorage.getHandler<MeshSwapper>(UserSettings.getAlternativeCensorImage() ?? MediaIdentifiers.CENSORED)!;
                    
                    Plugin.logIfDebugging(source => source.LogInfo($"The given entry {id} has been censored due to being blacklisted!"));
                }
                
                obj.setSwapperHolder(mesh, i, material, setAction, handler);

                Plugin.logIfDebugging(source => source.LogInfo($"Set Clients Material: {id}"));
            });
        });
    }
    
    public static bool doseObjectSupportMeshSwapping(Level level, GameObject gameObject) => searchMaterial(level, gameObject, (_, _, _, _, _) => true);

    public delegate bool MeshEntryMatcher(ICollection<Identifier> queryIds, MeshRenderer renderer, int materialIndex, Material material, Action<Material> setCallback);
    public delegate void MeshEntryHandler(ICollection<Identifier> queryIds, MeshRenderer renderer, int materialIndex, Material material, Action<Material> setCallback);
    
    public static void tryToAdjustMaterial(Level level, GameObject gameObject, MeshEntryHandler handler) { 
        searchMaterial(level, gameObject, (ids, mesh, i, material, setAction) => {
            handler(ids, mesh, i, material, setAction);

            return false;
        });
    }
    
    public static bool searchMaterial(Level level, GameObject gameObject, MeshEntryMatcher matcher) {
        //var pictureTargets = Plugin.config.textureSwapTargets().Select(RegexUtils.parseRegexWithFlags).ToList();
        
        // Traversing all MeshRenderers of the object
        foreach (var mesh in gameObject.GetComponents<MeshRenderer>()) {
            // Storing the shared materials of the MeshRenderer
            var sharedMaterials = mesh.sharedMaterials;

            if (sharedMaterials == null) continue;

            // Traversing all shared materials of the MeshRenderer
            for (int i = 0; i < sharedMaterials.Length; i++) {
                var material = sharedMaterials[i];
                
                if (material == null) continue;
                
                var match = TargetHandling.isObjectATarget(Plugin.getSwapperTargets(), level, gameObject, mesh, i, material, out var queryIds);

                // var match = pictureTargets.Any(regex => regex.IsMatch(name)) 
                //     || pictureTargets.Any(regex => regex.IsMatch(mesh.gameObject.name))
                //     || pictureTargets.Any(regex => regex.IsMatch(mesh.name));
                    
                if (!match) continue;
                
                var targetIndex = i;

                void setMaterialCallback(Material newMaterial) {
                    sharedMaterials[targetIndex] = newMaterial;

                    // Applying custom materials
                    mesh.sharedMaterials = sharedMaterials;
                }

                if (matcher(queryIds, mesh, targetIndex, material, setMaterialCallback)) return true;
            }
        }
        
        return false;
    }

    public delegate bool CensorshipTester(MediaQueryResult result);

    public static event CensorshipTester? IS_CENSORED_EVENT;
    
    public static bool isCensored(MediaQueryResult? queryResult) {
        if (Plugin.config.shouldRestrictQueries() && queryResult is RatedMediaResult queryRating && !queryRating.isSafe()) {
            return true;
        }
        
        if (queryResult is TaggedMediaResult taggedMediaResult) {
            foreach (var tag in Plugin.config.blacklistedTags()) {
                if (taggedMediaResult.hasTag(tag)) return true;
            }
        }
        
        return queryResult != null && (IS_CENSORED_EVENT?.Invoke(queryResult) ?? false);
    }
}

