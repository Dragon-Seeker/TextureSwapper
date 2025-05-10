using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using io.wispforest.textureswapper.api.components;
using io.wispforest.textureswapper.api.query;
using io.wispforest.util;
using MonoMod.Utils;
using io.wispforest.textureswapper.utils;
using Unity.VisualScripting;
using UnityEngine;

namespace io.wispforest.textureswapper.api;

public class MediaSwapperStorage {
    
    private static readonly SortedSet<Identifier> ALL_MEDIA_IDS = new (new IdentifierComparer());

    private static readonly ConcurrentQueue<Identifier> TO_BE_ADDED_IDS = new ();

    private static readonly Dictionary<Identifier, System.Collections.Generic.ISet<Action>> WAITING_TO_LOADED = new ();
    
    private static readonly ConcurrentDictionary<Identifier, ProcessingState> ID_TO_STATE = new ();
    
    private static readonly ConcurrentStack<RawMediaData> TO_BE_LOADED_QUEUE = new ();
    
    private static readonly ConcurrentDictionary<Identifier, MediaType> ID_TO_MEDIA_TYPE = new ();
    
    // TODO: PUSH INTERACTION WITH THESE OBJECTS TO MAIN THREAD OR NO?
    private static readonly ConcurrentDictionary<Identifier, MediaInfo> ID_TO_MEDIA_INFO = new ();
    private static readonly ConcurrentDictionary<Identifier, MediaQueryResult> ID_TO_MEDIA_QUERY_RESULT = new ();
    private static readonly ConcurrentDictionary<Identifier, SwapperBase> ID_TO_SWAPPER = new ();
    
    //--

    public static MediaType getMediaType(Identifier identifier) {
        return ID_TO_MEDIA_TYPE.ContainsKey(identifier) ? ID_TO_MEDIA_TYPE[identifier] : MediaType.UNKNOWN;
    }

    public static bool addIdAndTryToSetupType(string uri) {
        try {
            var id = Identifier.ofUri(uri);

            if (!ALL_MEDIA_IDS.Contains(id)) {
                var type = MediaFormats.getType(uri);
            
                TO_BE_ADDED_IDS.Enqueue(id);
                ID_TO_STATE[id] = ProcessingState.QUERIED;
                
                ID_TO_MEDIA_TYPE[id] = type;
                
                // TODO: UNHARDCODE THIS?
                if (type.Equals(MediaType.VIDEO) || type.Equals(MediaType.IMAGE)) {
                    ID_TO_SWAPPER[id] = new DelayedMeshSwapper(id, !type.Equals(MediaType.VIDEO));
                } else if(type.Equals(MediaType.AUDIO)) {
                    ID_TO_SWAPPER[id] = new DelayedGeneralSwapper(id, false);
                } 
            }
            
            return true;
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to setup type info for the given url: {uri}");
            Plugin.Logger.LogError(e);
        }
        
        return false;
    }
    
    public static SwapperBase? getHandler(Identifier identifier) {
        return ID_TO_SWAPPER.GetValueOrDefault(identifier);
    }
    
    public static S? getHandler<S>(Identifier identifier) where S : SwapperBase {
        var swapper = ID_TO_SWAPPER.GetValueOrDefault(identifier);

        return (typeof(MeshSwapper).IsAssignableFrom(typeof(S))) ? (S) swapper : default;
    }
    
    public static bool hasMaterial(Identifier identifier) {
        return ALL_MEDIA_IDS.Contains(identifier);
    }
    
    public static MediaInfo? getInfo(Identifier identifier) {
        return ID_TO_MEDIA_INFO.GetValueOrDefault(identifier);
    }
    
    public static MediaQueryResult? getResult(Identifier identifier) {
        return ID_TO_MEDIA_QUERY_RESULT.GetValueOrDefault(identifier);
    }
    
    internal static Identifier? getOrThrowId(string name) {
        var id = getId(name);

        if (id is not null) return id;
        
        throw new NullReferenceException($"Unable to get the desired texture! [Name: {name}]");
    }

    public static Identifier? getId(string name) {
        var results = getIds(name);
        return results.Count > 0 ? results[0] : null;
    }

    public static IList<Identifier> getIds(string name) {
        return ALL_MEDIA_IDS.Where(id => id.Path.Equals(name)).ToList();
    }

    public static FullMediaData? getFullData(Identifier id) {
        return new FullMediaData(id, getInfo(id) ?? MediaInfo.ofError(""), getResult(id) ?? new EmptyQueryResult());
    }
    
    //--
    
    public static List<Identifier> getMaterials(params MediaType[] types) {
        return getMaterials(types, null);
    }

    public static List<Identifier> getMaterials(MediaType[] types, Func<Identifier, bool>? filterFunc) {
        var typesSet = new ReadOnlySet<MediaType>(types);
        var invalidNames = MediaIdentifiers.DEFAULT_DATA_VARIANTS;
        
        return ALL_MEDIA_IDS.Where(id => {
                    return !invalidNames.Contains(id) 
                           && typesSet.Contains(getMediaType(id)) 
                           && (filterFunc is null || filterFunc(id));
                })
                .ToList();
    }

    public static void getOrActWithHandler<S>(Identifier id, Action<S> onHandlerGet) where S : SwapperBase {
        var handlerId = id;
        
        if (id is null) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to act with handler [{id}] as its null for some reason!"));

            handlerId = MediaIdentifiers.ERROR;
        } else {
            if (ID_TO_STATE.ContainsKey(id)) {
                Plugin.logIfDebugging(source => source.LogWarning($"Will wait for Handler Swapper [{id}] as its loading!"));

                WAITING_TO_LOADED.computeIfAbsent(id, _ => new HashSet<Action>())
                        .Add(() => getOrActWithHandler(id, onHandlerGet));
                
                return;
            }
        
            if (!ID_TO_SWAPPER.ContainsKey(id)) {
                Plugin.logIfDebugging(source => source.LogError($"Unable to act with handler [{id}] as its not even being loaded!"));

                handlerId = MediaIdentifiers.ERROR;
            } else {
                Plugin.logIfDebugging(source => source.LogInfo($"Handler Swapper for [{id}] has been run!"));
            }
        }

        var handler = getHandler(handlerId)!;

        if (handler is not S s) {
            if (!handlerId.Equals(MediaIdentifiers.ERROR)) {
                Plugin.logIfDebugging(source => source.LogError($"Handler Swapper for [{id}] seems to be the incorrect type for the given Swap Action!"));
            }
        } else {
            try {
                onHandlerGet(s);
            } catch (Exception e) {
                Plugin.Logger.LogError($"Given handler action threw an exception [Id: {id}]");
                Plugin.Logger.LogError(e);

                if (s is MeshSwapper) {
                    onHandlerGet(getHandler<S>(MediaIdentifiers.ERROR));
                }
            }
        }
    }

    // TODO: REWORK TO HANDLE HTTP CLIENTS VIA STATIC FIELD OR SOMETHING INSTEAD OF REMAKING
    public static void loadIfNotFound(FullMediaData data) {
        if (ID_TO_MEDIA_INFO.ContainsKey(data.id)) return;
        
        ALL_MEDIA_IDS.Add(data.id);
        
        if (data.isError()) {
            ID_TO_SWAPPER[data.id] = ID_TO_SWAPPER[MediaIdentifiers.ERROR] ?? new EmptySwapper(data.id);
            ID_TO_MEDIA_TYPE[data.id] = ID_TO_SWAPPER.ContainsKey(MediaIdentifiers.ERROR) ? MediaType.IMAGE : MediaType.UNKNOWN;
            
            Plugin.Logger.LogError($"Full Media Data from server for the given id [{data.id}] was found to be errored and can not be handled!");
            
            return;
        }
        
        ID_TO_MEDIA_INFO[data.id] = data.info;
        ID_TO_MEDIA_QUERY_RESULT[data.id] = data.result;

        ID_TO_STATE[data.id] = ProcessingState.QUERIED;
        
        ID_TO_MEDIA_TYPE[data.id] = data.info.format.getType();
        
        MultiThreadHelper.run(SemaphoreIdentifier.createFromMedia(data.info.uri), () => {
            var client = HttpClientUtils.createClient();
            
            RawMediaData.getData(client, data.info, data.result).ContinueWith((task) => {
                var results = task.Result;
                
                client.Dispose();

                if (task.IsCompletedSuccessfully && results is not null && !results.isError()) {
                    storeRawMediaData(results);
                } else {
                    Plugin.Logger.LogError($"Unable to get the image data for the following: {data.id}");
                    ID_TO_SWAPPER[data.id] = ID_TO_SWAPPER[MediaIdentifiers.MISSING];
                }

                liftIfSameState(data.id, ProcessingState.QUERIED);
            });
        });
    }

    private static void liftIfSameState(Identifier id, ProcessingState? state) {
        Plugin.logIfDebugging(source => source.LogWarning($"Was attempting to lift state for [{id}]: {state}" ));

        if (!ID_TO_STATE.TryGetValue(id, out var value)) return;
        
        if (state is null || value.Equals(state)) {
            ID_TO_STATE.Remove(id, out _);
        }
    }

    public static void storeRawMediaData(RawMediaData rawMediaData) {
        liftIfSameState(rawMediaData.id, ProcessingState.QUERIED);

        if (rawMediaData is null) {
            Plugin.logIfDebugging(source => source.LogWarning($"Was attempting to process some RawMediaData that was null!"));

            return;
        }

        var id = rawMediaData.id;

        ID_TO_MEDIA_TYPE[id] = rawMediaData.mediaInfo.format.getType();

        if (!Plugin.isMainThread()) {
            TO_BE_LOADED_QUEUE.Push(rawMediaData);
            ID_TO_STATE[id] = ProcessingState.LOADED;

            ID_TO_MEDIA_INFO[id] = rawMediaData.mediaInfo;
            ID_TO_MEDIA_QUERY_RESULT[id] = rawMediaData.queryResult;

            Plugin.logIfDebugging(source => source.LogWarning($"Will load this later on thread! {rawMediaData.id}"));

            return;
        }

        Plugin.logIfDebugging(source => source.LogInfo($"Attempting to load! {rawMediaData.id}"));

        if (isMediaDataErrored(rawMediaData)) return;

        var type = rawMediaData.mediaInfo.format;

        ID_TO_STATE[rawMediaData.id] = ProcessingState.PROCESSED;
        
        try {
            var handler = type.decodeData(rawMediaData.id, rawMediaData);
            
            if (isMediaDataErrored(rawMediaData)) return;
            
            storeHandler(id, rawMediaData.mediaInfo, handler);
        } catch (Exception e) {
            Plugin.logIfDebugging(source => {
                source.LogError($"Was unable to decode the given raw media data [{rawMediaData.id}] as an error occured:");
                source.LogError(e);
            });
            
            createErroredHandler(rawMediaData.id);
        }
    }

    private static bool isMediaDataErrored(RawMediaData data) {
        if (!data.isError()) return false;
        
        createErroredHandler(data.id);
            
        return true;
    }

    private static void createErroredHandler(Identifier id) {
        var deferredId = MediaIdentifiers.ERROR;
            
        Plugin.logIfDebugging(source => source.LogError($"Deferred to error! {deferredId}"));
            
        ID_TO_SWAPPER[id] = ID_TO_SWAPPER[deferredId] ?? new EmptySwapper(id);
    }

    public static void storeHandler(Identifier id, MediaInfo info, SwapperImpl? handler) {
        if (handler is IsDelayed) {
            Plugin.logIfDebugging(source => source.LogInfo($"Image was promised to be loaded later! {id}"));

            ID_TO_SWAPPER[id] = handler;
            
            return;
        }
        
        liftIfSameState(id, null);
        
        if (handler is not null) {
            Plugin.logIfDebugging(source => source.LogInfo($"Image has been loaded! {id}"));

            ID_TO_MEDIA_INFO[id] = info;
            ID_TO_SWAPPER[id] = handler;
        } else {
            Plugin.logIfDebugging(source => source.LogError($"Unable to decode! {id}"));
            
            ID_TO_SWAPPER[id] = ID_TO_SWAPPER[MediaIdentifiers.ERROR] ?? new EmptySwapper(id);
        }
    }

    internal static void handleToBeStoredHandlers() {
        if (!Plugin.isMainThread()) return;

        var ranActionIds = new HashSet<Identifier>();
        
        foreach (var entry in WAITING_TO_LOADED) {
            var id = entry.Key;

            if (ID_TO_STATE.ContainsKey(id)) continue;

            foreach (var action in entry.Value) {
                action();
            }

            ranActionIds.Add(id);
        }
        
        foreach (var ranActionId in ranActionIds) WAITING_TO_LOADED.Remove(ranActionId);
        
        if (!TO_BE_LOADED_QUEUE.IsEmpty) {
            var count = Math.Min(25, TO_BE_LOADED_QUEUE.Count);
            var batchedLoading = new RawMediaData[count];
            
            var poppedAmt = TO_BE_LOADED_QUEUE.TryPopRange(batchedLoading, 0, count);
            
            if (poppedAmt > 0) {
                foreach (var data in batchedLoading) {
                    storeRawMediaData(data);
                    liftIfSameState(data.id, ProcessingState.LOADED);
                }
            }
        }

        // TODO: MAY BE A ISSUE IF THE GIVEN CLEAR CALL OCCURS AFTER AN ADDITION TO THE QUEUE SO MAYBE LOCK THE SET OR SOMETHING INSTEAD?
        if (!TO_BE_ADDED_IDS.IsEmpty) {
            ALL_MEDIA_IDS.AddRange(TO_BE_ADDED_IDS);
            
            TO_BE_ADDED_IDS.Clear();
        }
    }
}

public class IdentifierComparer : IComparer<Identifier> {
    public int Compare(Identifier? id1, Identifier? id2) {
        if (ReferenceEquals(id1, id2)) return 0;
        if (id2 is null) return 1;
        if (id1 is null) return -1;

        return StringComparer.Ordinal.Compare(id1.ToString(), id2.ToString());
    }
}

public enum ProcessingState {
    QUERIED,
    LOADED,
    PROCESSED
}