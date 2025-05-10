using System;
using System.Collections.Concurrent;

namespace io.wispforest.textureswapper.utils;

public static class MainThreadHelper {
    private static readonly ConcurrentStack<Action> MAIN_THREAD_ACTIONS = new ();

    internal static void handleActionsOnMainThread() {
        if (!Plugin.isMainThread() || MAIN_THREAD_ACTIONS.IsEmpty) return;
        
        var batchSize = Math.Min(25, MAIN_THREAD_ACTIONS.Count);

        var batchedActions = new Action[batchSize];
            
        MAIN_THREAD_ACTIONS.TryPopRange(batchedActions, 0, batchSize);
            
        foreach (var batchedAction in batchedActions) {
            try {
                batchedAction();
            } catch (Exception e) {
                Plugin.logIfDebugging(source => {
                    source.LogError("Unable handle a given enqueued action on the main thread due to it throwing an exception!");
                    source.LogError(e);
                });
            }
        }
    }
    
    public static void runOnMainThread(Action action) {
        if (Plugin.isMainThread()) {
            action();
        } else {
            MAIN_THREAD_ACTIONS.Push(action);
        }
    }
}