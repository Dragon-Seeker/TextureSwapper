using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using io.wispforest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace io.wispforest.textureswapper.endec.format.newtonsoft;

public class JsonUtils {
    public static string writeToString(JToken value) {
        var stream = new StringWriter();
        
        value.WriteTo(new JsonTextWriter(stream));

        return stream.ToString();
    }
    
    public static JToken readFromString(string value) {
        return JToken.ReadFrom(new JsonTextReader(new StringReader(value)));
    }

    public static JToken readFromFile(string path) {
        return readFromString(File.ReadAllText(path));
    }
    
    public static JToken readFromStream(Stream value) {
        return readFromString(new StreamReader(value).ReadToEnd());
    }
    
    public static JToken readFromFileIfPresent(string path) {
        if (!File.Exists(path)) return new JObject();
        
        return readFromString(File.ReadAllText(path));
    }

    public static T parseFromFile<T>(string path) where T : EndecGetter<T> {
        return parseFromFile<T>(path, EndecGetter.Endec<T>());
    }
    
    public static T? parseFromFile<T>(string path, Endec<T> endec) {
        if (!File.Exists(path)) return default;
        
        var json = readFromFile(path);

        return endec.decodeFully(JsonDeserializer.of, json);
    }
    
    public static T parseFromStream<T>(Stream stream) where T : EndecGetter<T> {
        return parseFromStream<T>(stream, EndecGetter.Endec<T>());
    }
    
    public static T parseFromStream<T>(Stream stream, Endec<T> endec) {
        var json = readFromStream(stream);

        return endec.decodeFully(JsonDeserializer.of, json);
    }
    
    public static T parseFromString<T>(string str, Endec<T> endec) {
        var json = readFromString(str);

        return endec.decodeFully(JsonDeserializer.of, json);
    }
    
    public static Dictionary<string,T> parseFiles<T>(string directory, Func<string, string> keyMaker, Action<string, Exception> onError) where T : EndecGetter<T> {
        return parseFiles<T>(directory, EndecGetter.Endec<T>(), keyMaker, onError);
    }

    private static readonly List<string> ALLOWED_JSON_PATTERNS = ["*.json", "*.json5"];
    
    public static Dictionary<string, T> parseFiles<T>(string directory, Endec<T> endec, Func<string, string> keyMaker, Action<string, Exception> onError) {
        var files = ALLOWED_JSON_PATTERNS.SelectMany(pattern => Directory.GetFiles(directory, pattern)).ToList();

        var entries = new Dictionary<string, T>();
        
        foreach (var file in files) {
            if (file is null || !File.Exists(file)) continue;

            try {
                var entry = parseFromFile(file, endec);

                entries[keyMaker(file)] = entry;
            } catch (Exception e) {
                onError(file, e);
            }
        }

        return entries;
    }
}