using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using io.wispforest.textureswapper.api;

namespace io.wispforest.textureswapper.utils;

public class HttpClientUtils {

    public static HttpClient createClient() {
        var client = new HttpClient();
        
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RepoTextureSwapper/1.0 (by Blodhgarm on github)");

        return client;
    }
    
    public static async Task<string?> getContentTypeFromUrlAsync(string url) {
        using var client = createClient();
        
        try {
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            
            response.EnsureSuccessStatusCode();
            
            if (response.Content != null && response.Content.Headers != null) {
                return response.Content?.Headers?.ContentType?.MediaType.Split("/")[1];
            }
        } catch (Exception ex)  {
            Plugin.logIfDebugging(source => source.LogError($"Unable to get the content type from the URL [{url}]: {ex}"));
        }
        
        return null;
    }

    public static string getFormatString(string url, int timeOutWindow = 30) {
        var dataGrabTask = getContentTypeFromUrlAsync(url);
        var delayTask = Task.Delay(timeOutWindow * 1000);

        var completedTaskIndex = Task.WaitAny([dataGrabTask, delayTask]);
        
        var hasResponse = completedTaskIndex == 0 && dataGrabTask.IsCompletedSuccessfully;

        string? format = null;
        
        if (hasResponse) format = dataGrabTask.Result;

        return format ?? Path.GetExtension(url).Replace(".", "");
    }
    
    public static async void iteratePosts<T>(string type, int delayBetweenTask, HttpClient client, ConcurrentQueue<T> entries, Func<HttpClient, T, int, Task> taskCreator, Func<T, string> toURL)  {
        try {
            var tasks = new ConcurrentQueue<Task>();

            while (entries.TryDequeue(out var entry)) {
                var url = toURL(entry);
                
                tasks.Enqueue(MultiThreadHelper.run(SemaphoreIdentifier.createFromMedia(url), () => taskCreator(client, entry, 0)));

                Plugin.logIfDebugging(source => source.LogInfo($"Task for Entry Decode has been created: {url}"));
                
                Thread.Sleep(delayBetweenTask);
            }

            await Task.WhenAll(tasks.ToArray()).ContinueWith(_ => {
                Plugin.logIfDebugging(source => source.LogInfo($"Disposing of HTTP Client!"));
                client.Dispose();
            });

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