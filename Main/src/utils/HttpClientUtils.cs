using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using io.wispforest.textureswapper.api;
using io.wispforest.textureswapper.api.core;

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
        return id.Equals(other.id);
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
            SslOptions = new SslClientAuthenticationOptions() {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12,
            },
            MaxConnectionsPerServer = this.maxConnectionsPerServer, // Default MaxConnectionsPerServer is effectively infinite
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(this.idleTimeoutMinutes),
            PooledConnectionLifetime = TimeSpan.FromMinutes(this.lifetimeMinutes),
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(30)
        };
    }
}

public static class HttpClientUtils {

    private static readonly ConcurrentDictionary<HttpClientKey, HttpClient> CLIENTS = new ();

    public static readonly HttpClientKey GENERAL_KEY = new (Identifier.of(Plugin.id, "general"), 10);

    static HttpClientUtils() {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }
    
    public static HttpClient getOrCreateClient(HttpClientKey? key = null) => CLIENTS.computeIfAbsent(key ?? GENERAL_KEY, (key) => {
        var client = new HttpClient(key.createHandler());

        client.DefaultRequestHeaders.UserAgent.ParseAdd("TextureSwapper/1.2.0 (by Blodhgrm on e621; author=Blodhgarm; github=https://github.com/Dragon-Seeker/TextureSwapper;)");

        return client;
    });
    
    public static async Task<string?> getFormatAsStringAsync(string url) {
        var client = getOrCreateClient(GENERAL_KEY);
        
        try {
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            
            response.EnsureSuccessStatusCode();

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
    
    public delegate Task DataHandler<in K, in T>(HttpClient client, K key, T data, int currentTry);
    
    public delegate Task DataHandler<in T>(HttpClient client, T data, int currentTry);

    public delegate string UrlGetter<in T>(T data);

    public static async Task iteratePosts<K, T>(this HttpClient client, string type, int delayBetweenTask, ConcurrentQueue<T> entries, K k, DataHandler<K, T> handler, UrlGetter<T> getter) {
        await client.iteratePosts(type, delayBetweenTask, entries, (client, data, currentTry) => handler(client, k, data, currentTry), getter);
    }

    public static async Task iteratePosts<T>(this HttpClient client, string type, int delayBetweenTask, ConcurrentQueue<T> entries, DataHandler<T> handler, UrlGetter<T> getter)  {
        try {
            var tasks = new ConcurrentQueue<Task>();

            while (entries.TryDequeue(out var entry)) {
                var url = getter(entry);
                
                tasks.Enqueue(MultiThreadHelper.run(SemaphoreIdentifier.createFromMedia(url), () => handler(client, entry, 0)));

                Plugin.logIfDebugging(source => source.LogInfo($"Task for Entry Decode has been created: {url}"));
                
                Thread.Sleep(delayBetweenTask);
            }

            await Task.WhenAll(tasks.ToArray());

            Plugin.logIfDebugging(source => source.LogInfo($"Created all {type} tasks for a given query!"));
        } catch (Exception e) {
            Plugin.logIfDebugging(source => {
                source.LogInfo($"Exception when trying to handle {type} entries for a given query!");
                source.LogInfo(e);
            });
        }
    }
    
    public static bool waitOrLog(Task task, int seconds, Func<string> logMsg) {
        if (task.Wait(seconds * 1000)) return false;
        
        Plugin.Logger.LogError(logMsg());
            
        return true;
    }

}