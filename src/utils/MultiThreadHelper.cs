using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using io.wispforest.textureswapper.api;
using io.wispforest.textureswapper.api.query;

namespace io.wispforest.textureswapper.utils;

public class MultiThreadHelper {

    private static bool shouldPrintDebugInfo() => false;

    public static readonly SemaphoreIdentifier DEFAULT_GROUP = new SemaphoreIdentifier(
            Identifier.of("texture_swapper", "default"), 
            maxCount: 1
    );
    
    public static readonly MultiThreadHelper INSTANCE = new (1);

    private readonly Dictionary<SemaphoreIdentifier, SemaphoreSlim> ID_TO_SEMAPHORE = new ();

    public MultiThreadHelper(int defaultMaxConcurrency) {
        ID_TO_SEMAPHORE[DEFAULT_GROUP] = new SemaphoreSlim(defaultMaxConcurrency, defaultMaxConcurrency);
    }

    public static Task run(Action action) {
        return run(DEFAULT_GROUP, action);
    }

    public static Task run(SemaphoreIdentifier id, Action action) {
        return INSTANCE.runAndExecuteAsync(id, action);
    }

    public Task runAndExecuteAsync(SemaphoreIdentifier id, Action action) {
        var semaphore = ID_TO_SEMAPHORE.computeIfAbsent(id, id1 => id1.createSemaphore());
        
        return Task.Run(async () => {
            await executeAsync(semaphore, () => {
                action();

                return Task.CompletedTask;
            });
        });
    }
    
    public static async Task executeAsync(SemaphoreSlim semaphore, Func<Task> taskDelegate) {
        Plugin.logIfDebugging(source => source.LogInfo("Going to check flag barrier"), predicate: shouldPrintDebugInfo);
        
        await semaphore.WaitAsync(); // Acquire a permit

        Plugin.logIfDebugging(source => source.LogInfo("Starting Task"), predicate: shouldPrintDebugInfo);
        
        try {
            await taskDelegate(); // Execute the task
            
            Plugin.logIfDebugging(source => source.LogInfo("Task Has been finished"), predicate: shouldPrintDebugInfo);
        } finally {
            semaphore.Release(); // Release the permit
            
            Plugin.logIfDebugging(source => source.LogInfo("Permit has been released"), predicate: shouldPrintDebugInfo);
        }
    }

    public static async Task<TResult> executeAsync<TResult>(SemaphoreSlim semaphore, Func<Task<TResult>> taskDelegate) {
        Plugin.logIfDebugging(source => source.LogInfo("Going to check flag barrier"), predicate: shouldPrintDebugInfo);
        
        await semaphore.WaitAsync(); // Acquire a permit

        Plugin.logIfDebugging(source => source.LogInfo("Starting Task"), predicate: shouldPrintDebugInfo);
        
        try {
            return await taskDelegate(); // Execute the task
        } finally {
            Plugin.logIfDebugging(source => source.LogInfo("Task Has been finished"), predicate: shouldPrintDebugInfo);
            
            semaphore.Release(); // Release the permit
            
            Plugin.logIfDebugging(source => source.LogInfo("Permit has been released"), predicate: shouldPrintDebugInfo);
        }
    }
}

public class SemaphoreIdentifier {
    
    public readonly Identifier identifier;
    
    private readonly int initialCount;
    private readonly int maxCount;

    public SemaphoreIdentifier(Identifier identifier, int maxCount = 5, int initialCount = -1) {
        this.identifier = identifier;
        this.maxCount = maxCount;
        this.initialCount = initialCount == -1 ? maxCount : initialCount;
    }

    public SemaphoreSlim createSemaphore() {
        return new SemaphoreSlim(initialCount, maxCount);
    }

    public static SemaphoreIdentifier createFromMedia(string url) {
        var id = Identifier.ofUri(url);
        
        return new SemaphoreIdentifier(Identifier.of("texture_swapper", id.Namespace), maxCount: 4);
    }
    
    protected bool Equals(SemaphoreIdentifier other) => identifier.Equals(other.identifier);

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((SemaphoreIdentifier)obj);
    }

    public override int GetHashCode() => identifier.GetHashCode();
}