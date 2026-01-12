using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using io.wispforest.textureswapper.api;

namespace io.wispforest.textureswapper.utils;

public class HttpClientKey(Identifier id, int maxConnectionsPerServer, int idleTimeoutMinutes = 2, int lifetimeMinutes = 15) {
    
    public readonly Identifier id = id;
    
    private readonly int maxConnectionsPerServer = maxConnectionsPerServer;
    private readonly int idleTimeoutMinutes = idleTimeoutMinutes;
    private readonly int lifetimeMinutes = lifetimeMinutes;

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not HttpClientKey other) return false;
        return id.Equals(other.id) 
               && maxConnectionsPerServer == other.maxConnectionsPerServer
               && idleTimeoutMinutes == other.idleTimeoutMinutes
               && lifetimeMinutes == other.lifetimeMinutes;
    }

    public override int GetHashCode() {
        unchecked {
            return (id.GetHashCode() * 397) ^ maxConnectionsPerServer;
        }
    }

    public static bool operator ==(HttpClientKey? left, HttpClientKey? right) => Equals(left, right);

    public static bool operator !=(HttpClientKey? left, HttpClientKey? right) => !Equals(left, right);

    public StandardSocketsHttpHandler createHandler() {
        return new StandardSocketsHttpHandler { 
            MaxConnectionsPerServer = this.maxConnectionsPerServer, // Default MaxConnectionsPerServer is effectively infinite
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(this.idleTimeoutMinutes),
            PooledConnectionLifetime = TimeSpan.FromMinutes(this.lifetimeMinutes),
            UseCookies = false,
            
        };
    }
}

public static class HttpClientUtils {

    private static readonly ConcurrentDictionary<HttpClientKey, HttpClient> CLIENTS = new ();

    public static readonly HttpClientKey GENERAL_KEY = new (Identifier.of(Plugin.id, "general"), 8);

    public static HttpClient getOrCreateClient(HttpClientKey? key = null) => CLIENTS.computeIfAbsent(key ?? GENERAL_KEY, (key) => {
        var client = new HttpClient(key.createHandler());

        client.DefaultRequestHeaders.UserAgent.ParseAdd("RepoTextureSwapper/1.0 (by Blodhgarm on github)");

        return client;
    });
    
    public static async Task<string?> getFormatAsStringAsync(string url) {
        using var client = getOrCreateClient(GENERAL_KEY);
        
        try {
            var response = (await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url)))
                    .EnsureSuccessStatusCode();

            var headers = response.Content?.Headers;
            
            if (headers != null) return headers.ContentType?.MediaType.Split("/")[1];
        } catch (Exception ex)  {
            Plugin.logIfDebugging(source => source.LogError($"Unable to get the content type from the URL [{url}]: {ex}"));
        }
        
        return null;
    }

    public static string getFormatAsString(string url, int timeOutWindow = 30) {
        var dataGrabTask = getFormatAsStringAsync(url);

        var completedTaskIndex = Task.WaitAny([dataGrabTask, Task.Delay(timeOutWindow * 1000)]);
        
        var hasResponse = completedTaskIndex == 0 && dataGrabTask.IsCompletedSuccessfully;

        return (hasResponse ? dataGrabTask.Result : null) ?? UriUtils.getFormatFromUri(url);
    }
    
    public static async void iteratePosts<T>(this HttpClient client, string type, int delayBetweenTask,  ConcurrentQueue<T> entries, Func<HttpClient, T, int, Task> taskCreator, Func<T, string> toURL)  {
        try {
            var tasks = new ConcurrentQueue<Task>();

            while (entries.TryDequeue(out var entry)) {
                var url = toURL(entry);
                
                tasks.Enqueue(MultiThreadHelper.run(SemaphoreIdentifier.createFromMedia(url), () => taskCreator(client, entry, 0)));

                Plugin.logIfDebugging(source => source.LogInfo($"Task for Entry Decode has been created: {url}"));
                
                Thread.Sleep(delayBetweenTask);
            }

            // await Task.WhenAll(tasks.ToArray()).ContinueWith(_ => {
            //     Plugin.logIfDebugging(source => source.LogInfo($"Disposing of HTTP Client!"));
            //     client.Dispose();
            // });

            Plugin.logIfDebugging(source => source.LogInfo($"Created all {type} tasks for a given query!"));
        } catch (Exception e) {
            Plugin.logIfDebugging(source => source.LogInfo($"Exception when trying to handle {type} entries for a given query!"));
        }
    }
    
    public static bool waitOrLog(Task task, int seconds, Func<string> logMsg) {
        if (task.Wait(seconds * 1000)) return false;
        
        Plugin.Logger.LogError(logMsg());
            
        return true;
    }

}