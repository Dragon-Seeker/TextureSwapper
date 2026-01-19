using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using io.wispforest.textureswapper.api;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query;
using UnityEngine;

namespace io.wispforest.textureswapper.utils;

public class MultiThreadHelper {

    private static bool shouldPrintDebugInfo() => false;

    public static readonly SemaphoreIdentifier DEFAULT_GROUP = new (
            Identifier.of("texture_swapper", "default"), 
            maxCount: 1
    );
    
    public static readonly MultiThreadHelper INSTANCE = new ();

    private readonly Dictionary<SemaphoreIdentifier, SemaphoreSlim> ID_TO_SEMAPHORE = new ();
    private readonly ConcurrentDictionary<SemaphoreIdentifier, ConcurrentDictionary<Guid, TaskState>> ID_TO_CURRENT_TASKS = new();

    private MultiThreadHelper() {
        ID_TO_SEMAPHORE[DEFAULT_GROUP] = DEFAULT_GROUP.createOrGetSemaphore();
    }

    public static Task run(Action action) => run(DEFAULT_GROUP, action);
    
    public static Task run(SemaphoreIdentifier id, Action action) {
        return INSTANCE.runAndExecuteAsync(id, () => {
            action();
            
            return Task.CompletedTask;
        });
    }
    
    public static Task run(SemaphoreIdentifier id, Func<Task> func) => INSTANCE.runAndExecuteAsync(id, func);

    public Task runAndExecuteAsync(SemaphoreIdentifier id, Func<Task> func) {
        var semaphore = ID_TO_SEMAPHORE.computeIfAbsent(id, id1 => id1.createOrGetSemaphore());
        var guid = Guid.NewGuid();

        var state = getOrCreateState(id, guid);
        
        var task = Task.Run(async () => {
            await executeAsync(id, guid, semaphore, func);

            ID_TO_CURRENT_TASKS[id].removeIfPresent(guid);
        });
        
        state.setTask(task);
        
        return task;
    }
    
    public Task<T> runAndExecuteAsync<T>(SemaphoreIdentifier id, Func<T> action) {
        var semaphore = ID_TO_SEMAPHORE.computeIfAbsent(id, id1 => id1.createOrGetSemaphore());
        var guid = Guid.NewGuid();

        var state = getOrCreateState(id, guid);
        
        var task = Task.Run(async () => {
            var result = await executeAsync<T>(id, guid, semaphore, () => Task.FromResult(action()));

            ID_TO_CURRENT_TASKS[id].removeIfPresent(guid);
            
            return result;
        });
        
        state.setTask(task);
        
        return task;
    }

    private TaskState getOrCreateState(SemaphoreIdentifier id, Guid guid) {
        return ID_TO_CURRENT_TASKS.computeIfAbsent(id, _ => new ConcurrentDictionary<Guid, TaskState>())
                .computeIfAbsent(guid, guid1 => new TaskState(guid1));
    }
    
    private async Task executeAsync(SemaphoreIdentifier id, Guid guid, SemaphoreSlim semaphore, Func<Task> taskDelegate) {
        Plugin.logIfDebugging(source => source.LogInfo("Going to check flag barrier"), predicate: shouldPrintDebugInfo);
        
        var state = getOrCreateState(id, guid);
        
        state.setStage(Stage.WAITING);
        
        await semaphore.WaitAsync(); // Acquire a permit

        Plugin.logIfDebugging(source => source.LogInfo("Starting Task"), predicate: shouldPrintDebugInfo);
        
        try {
            state.setStage(Stage.EXECUTION);
            
            await taskDelegate(); // Execute the task
        } finally {
            state.setStage(Stage.FINISH);
            
            Plugin.logIfDebugging(source => source.LogInfo("Task Has been finished"), predicate: shouldPrintDebugInfo);
            
            if (!id.hasManagedRelease) semaphore.Release(); // Release the permit
            
            Plugin.logIfDebugging(source => source.LogInfo("Permit has been released"), predicate: shouldPrintDebugInfo);
        }
    }

    private async Task<TResult> executeAsync<TResult>(SemaphoreIdentifier id, Guid guid, SemaphoreSlim semaphore, Func<Task<TResult>> taskDelegate) {
        Plugin.logIfDebugging(source => source.LogInfo("Going to check flag barrier"), predicate: shouldPrintDebugInfo);
        
        var state = getOrCreateState(id, guid);
        
        state.setStage(Stage.WAITING);
        
        await semaphore.WaitAsync(); // Acquire a permit

        Plugin.logIfDebugging(source => source.LogInfo("Starting Task"), predicate: shouldPrintDebugInfo);
        
        try {
            state.setStage(Stage.EXECUTION);
            
            return await taskDelegate(); // Execute the task
        } finally {
            state.setStage(Stage.FINISH);

            Plugin.logIfDebugging(source => source.LogInfo("Task Has been finished"), predicate: shouldPrintDebugInfo);
            
            if (!id.hasManagedRelease) semaphore.Release(); // Release the permit
            
            Plugin.logIfDebugging(source => source.LogInfo("Permit has been released"), predicate: shouldPrintDebugInfo);
        }
    }

    private const int MAX_ALOTTED_TIME = 600; // in Seconds

    private bool isPrunning = false;
    private Stopwatch prunningWait = Stopwatch.StartNew();
    
    internal void pruneTasks() {
        if (isPrunning) return;

        if (prunningWait.ElapsedMilliseconds / 1000 < MAX_ALOTTED_TIME) return;
        
        prunningWait.Restart();
        
        Task.Run(() => {
            isPrunning = true;
            ID_TO_CURRENT_TASKS.forEach((id, tasks) => {
                var statesToRemove = new HashSet<Guid>();
                
                tasks.forEach((guid, state) => {
                    var data = state.getCurrentTotalMillisecounds();
                    
                    if ((state.currentStage == Stage.FINISH) || (data.stage != Stage.WAITING && (data.total / 1000) > MAX_ALOTTED_TIME)) {
                        statesToRemove.Add(guid);
                    }
                });
                
                foreach (var guid in statesToRemove) {
                    tasks.removeIfPresent(guid);
                }
                
                Plugin.logIfDebugging(() => $"Pruned [{statesToRemove.Count}] tasks in the group [{id.identifier}]!");
            });
            isPrunning = false;
        });
    }
}

public class SemaphoreIdentifier(Identifier identifier, int maxCount = 5, int initialCount = -1, bool managedRelease = false) {
    
    public readonly Identifier identifier = identifier;
    
    private readonly int initialCount = initialCount == -1 ? maxCount : initialCount;

    private static readonly ConcurrentDictionary<Identifier, SemaphoreSlim> idToSlim = new ();
    
    private SemaphoreSlim? slim;

    public SemaphoreSlim createOrGetSemaphore() {
        slim ??= idToSlim.computeIfAbsent(identifier, () => new SemaphoreSlim(initialCount, maxCount));

        return slim;
    }

    public int getMaxCount => maxCount;

    public bool hasManagedRelease => managedRelease;

    public static SemaphoreIdentifier createFromMedia(string url) {
        var id = Identifier.ofUri(url);
        
        return new SemaphoreIdentifier(Identifier.of("texture_swapper", id.Namespace), maxCount: 4);
    }
    
    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is SemaphoreIdentifier other && identifier.Equals(other.identifier);
    }

    public override int GetHashCode() => identifier.GetHashCode();
}

public class TaskState {
    private readonly Guid guid;
    private Task? task;

    private Stopwatch stopwatch = Stopwatch.StartNew();

    public Stage currentStage { get; internal set; } = Stage.INIT;

    private ConcurrentDictionary<Stage, long> stages = new ();

    internal TaskState(Guid guid) {
        this.guid = guid;
    }

    internal void setStage(Stage stage) {
        stopwatch.Stop();
            
        stages[currentStage] = stopwatch.ElapsedMilliseconds;

        currentStage = stage;
        
        if (stage == Stage.FINISH) return;
        
        stopwatch.Restart();
    }

    internal void setTask(Task task) {
        this.task = task;
    }
    
    public (Stage stage, long total) getCurrentTotalMillisecounds() {
        return new (currentStage, stopwatch.ElapsedMilliseconds);
    }
    
    public long getTotalMillisecounds() {
        var baseTotal = stages.Values.Sum();

        if (currentStage != Stage.FINISH) {
            baseTotal += stopwatch.ElapsedMilliseconds;
        }
        
        return baseTotal;
    }

    public long getStateTotalMillisecounds(Stage stage) {
        if (currentStage == stage) return stopwatch.ElapsedMilliseconds;
        
        return stages.GetValueOrDefault(stage, 0);
    }

    private bool endTask() {
        if (task is null) return false;
        
        task.Dispose();
        
        stopwatch.Stop();
        
        return true;
    }
}

public enum Stage {
    INIT,
    WAITING,
    EXECUTION,
    FINISH
}