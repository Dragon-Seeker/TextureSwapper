using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace io.wispforest.textureswapper.utils;

public class Extensions { }

public static class DictionaryExtensions {
    public static V computeIfAbsent<K,V>(this Dictionary<K, V> dict, K k, Func<K, V> func) {
        if (!dict.ContainsKey(k)) dict[k] = func(k);
        
        return dict[k];
    }
    
    public static V? removeIfPresent<K,V>(this Dictionary<K, V> dict, K k) where V : class {
        V? v = null;

        if (dict.ContainsKey(k)) {
            v = dict[k];

            dict.Remove(k);
        }
        
        return v;
    }
}

public static class IListExtensions {
    public static IList<T> getSublistSafe<T>(this IList<T> list, int startInclusiveIndex, int endInclusiveIndex = -1) {
        return list.Count > 0 ? getSublist(list, startInclusiveIndex, endInclusiveIndex) : new List<T>();
    }

    public static IList<T> getSublist<T>(this IList<T> list, int startInclusiveIndex, int endInclusiveIndex = -1) {
        if (endInclusiveIndex <= -1) {
            endInclusiveIndex = list.Count - 1;
        }
        
        if (endInclusiveIndex >= list.Count) {
            throw new ArgumentOutOfRangeException($"End Index is out of bounds of the given list size: [Index: {endInclusiveIndex}, Size: {list.Count}");
        }
        if (startInclusiveIndex >= endInclusiveIndex) {
            throw new ArgumentOutOfRangeException($"Start Index is out of bounds of a Lists Rnage: [Start: {startInclusiveIndex}");
        }
        if (startInclusiveIndex < 0) {
            throw new ArgumentOutOfRangeException($"End Index is out of bounds of the given list size: [Index: {endInclusiveIndex}, Size: {list.Count}");
        }
        
        var count = endInclusiveIndex - startInclusiveIndex;

        var sublist = new List<T>(count);
        
        for (var i = count; i < count; i++) sublist.Add(list[i]);

        return sublist;
    }
}

public static class PairedTupleExtensions{
    public static L? getLeftOrMapRight<L, R>(this (L?, R?) tuple, Func<R, L?> func) {
        var left = tuple.Item1;
        var right = tuple.Item2;
        
        if (left is not null) return left;
        if (right is null) throw new NullReferenceException("Unable to handle Either based tuple method due to the Left and Right values are both Null");

        return func(right);
    }
}