using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BepInEx;
using io.wispforest.endec;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper.api.query.impl;

[BepInPlugin(SwapperPluginInfo.PLUGIN_GUID + "_web_addon", SwapperPluginInfo.PLUGIN_NAME + " Web Addon", "1.0.0")]
public class StaticWeb : BaseUnityPlugin {

    public static StaticWebQuery? CONFIG_QUERY;
    
    private void Awake() {
        init();
    }
    
    public static void init() {
        MediaQueryTypeRegistry.register<StaticWebQueryType, StaticWebQuery, StaticWebQueryResult>(StaticWebQueryType.INSTANCE);

        Plugin.ADDITIONAL_QUERY_LOOKUP += callback => {
            callback(StaticWebQueryType.ID, [getConfigQuery()]);
        };
    }

    public static StaticWebQuery getConfigQuery() {
        if (CONFIG_QUERY is null) {
            CONFIG_QUERY = StaticWebQuery.of(Identifier.of(Plugin.id, "config_query"), Plugin.config.staticWebMedia());
        }

        return CONFIG_QUERY;
    }
}

public class StaticWebQuery : MediaQuery, EndecGetter<StaticWebQuery> {

    public static readonly StructEndec<StaticWebQuery> ENDEC = StructEndecBuilder.of(
        Identifier.ENDEC.optionalFieldOf<StaticWebQuery>("id", s => s.key.id, () => null),
        endec.Endec.STRING.listOf().fieldOf<StaticWebQuery>("urls", s => s.urls),
        MediaRatingUtils.ENDEC.fieldOf<StaticWebQuery>("rating", s => s.rating),
        endec.Endec.STRING.listOf().optionalFieldOf<StaticWebQuery>("tags", s => s.tags, () => []),
        (id, urls, rating, tags) => new StaticWebQuery(id, urls, rating, tags)
    );

    public IList<string> urls { get; }
    public MediaRating rating { get; }
    public IList<string> tags { get; }
    
    private StaticWebQuery(Identifier? id, IList<string> urls, MediaRating rating, IList<string>? tags) : base(id) {
        this.urls = urls;
        this.rating = rating;
        this.tags = tags ?? [];
    }

    public static StaticWebQuery of(Identifier? id, IList<string> urls, MediaRating rating = MediaRating.SAFE, IList<string>? tags = null) => new(id, urls, rating, tags ?? []);
    
    public override Identifier queryTypeId => StaticWebQueryType.ID;
}

public class StaticWebQueryType : MediaQueryType<StaticWebQuery, StaticWebQueryResult> {
    public static readonly Identifier ID = Identifier.of("texture_swapper", "static_web");
    public static readonly StaticWebQueryType INSTANCE = new ();

    public override StructEndec<StaticWebQuery> getDataEndec() => StaticWebQuery.ENDEC;
    public override StructEndec<StaticWebQueryResult> getResultEndec() => StaticWebQueryResult.ENDEC;

    public override Identifier getLookupId() => ID;

    public override void executeQuery(StaticWebQuery data) {
        if (data.urls.Count <= 0) return;

        var adjustedUrls = data.urls.Select(adjustURL)
                .selectNonNull()
                .ToList();
        
        foreach (var url in adjustedUrls) {
            MediaSwapperStorage.addIdAndTryToSetupType(url);
        }

        var queue = new ConcurrentQueue<(string url, MediaRating rating, IList<string> tags)>(adjustedUrls.Select(s => (s, data.rating, data.tags)));

        var key = data.key;

        MediaSwapperStorage.getOrCreateQueryStorage(key).query = data;
        
        MultiThreadHelper.run(createSemaphoreIdentifier(), () => {
            HttpClientUtils.getOrCreateClient()
                    .iteratePosts("Web", 300, queue, key, handlePost, tuple => tuple.url);
        });
    }

    // TODO: USE THIS LOCATION TO ADJUST URLS though API and check blacklist
    private static string? adjustURL(string url) {
        try {
            if (url.Contains("https://www.reddit.com/media?url=")) {
                var mediaUrl = getUrlParameter(url, "url");

                if (mediaUrl is null) return null;
                
                return HttpUtility.UrlDecode(mediaUrl);
            }
        } catch (Exception e) {
            Plugin.Logger.LogWarning($"Unable to adjust the given url, such will be ignored: [Url: {url}]");
            Plugin.Logger.LogError(e);
        }
        
        return url;
    }
    
    private static string? getUrlParameter(string fullUrl, string parameterName) {
        return HttpUtility.ParseQueryString(new Uri(fullUrl).Query)
                .Get(parameterName);
    }

    private static async Task handlePost(HttpClient client, MediaQueryKey key, (string url, MediaRating rating, IList<string> tags) tuple, int currentTry) {
        var url = tuple.url;
        var queryResult = new StaticWebQueryResult(UriUtils.getDomain(url) ?? "unknown", tuple.rating, tuple.tags);
        
        try {
            RawMediaData.getWebData(client, queryResult, url).ContinueWith(async (imageTask) => {
                var IsCompletedSuccessfully = imageTask.IsCompletedSuccessfully;

                if (IsCompletedSuccessfully) {
                    var mediaData = imageTask.Result;

                    IsCompletedSuccessfully = !mediaData.isError();

                    if (IsCompletedSuccessfully) {
                        MediaSwapperStorage.storeRawMediaData(key, mediaData);
                        return;
                    }
                }

                if (currentTry < 1) {
                    Plugin.logIfDebugging(source =>
                            source.LogWarning($"Attempting to try again on handling Static Web Image [{url}] due to some unknown issue."));

                    Thread.Sleep(250);

                    await handlePost(client, key, tuple, currentTry + 1);
                }
                else {
                    Plugin.logIfDebugging(source => source.LogError($"Was unable to handle Static Web Image [{url}] due to some unknown issue."));
                }
            }).Wait();
        } catch (Exception e) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to handle Static Web Image [{url}] due to the given error: {e}"));
        }
    }
}

public class StaticWebQueryResult(string domain, MediaRating rating, IList<string> tags)
    : MediaQueryResult, EndecGetter<StaticWebQueryResult>, RatedMediaResult, TaggedMediaResult {
    public string domain { get; } = domain;
    public MediaRating rating { get; } = rating;
    public IList<string> tags { get; } = tags;

    public static readonly StructEndec<StaticWebQueryResult> ENDEC = StructEndecBuilder.of(
        endec.Endec.STRING.fieldOf<StaticWebQueryResult>("domain", s => s.domain),
        MediaRatingUtils.ENDEC.fieldOf<StaticWebQueryResult>("rating", s => s.rating),
        endec.Endec.STRING.listOf().fieldOf<StaticWebQueryResult>("tags", s => s.tags),
        (domain, rating, tags) => new StaticWebQueryResult(domain, rating, tags)
    );

    public static Endec<StaticWebQueryResult> Endec() => ENDEC;

    public override Identifier queryTypeId => StaticWebQueryType.ID;

    public bool hasTag(string tag) => this.tags.Contains(tag);

    public override void addToTooltip(StringBuilder builder) {
        builder.AppendLine($"Domain: {domain}");
        builder.AppendLine($"Rating: {rating}");
        builder.AppendLine($"Tags: {tags}");
    }
}