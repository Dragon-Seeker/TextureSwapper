using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.utils;

public class Extensions { }

public static class DictionaryExtensions {
    public static V computeIfAbsent<K,V>(this IDictionary<K, V> dict, K k, Func<V> func) {
        return computeIfAbsent(dict, k, _ => func());
    }
    
    public static V computeIfAbsent<K,V>(this IDictionary<K, V> dict, K k, Func<K, V> func) {
        if (!dict.ContainsKey(k)) dict[k] = func(k);
        
        return dict[k];
    }
    
    public static V? removeIfPresent<K,V>(this IDictionary<K, V> dict, K k) where V : class {
        V? v = null;

        if (dict.ContainsKey(k)) {
            v = dict[k];

            dict.Remove(k);
        }
        
        return v;
    }
    
    public static void forEach<K,V>(this IDictionary<K, V> dict, Action<K, V> func) {
        foreach (var keyValuePair in dict) {
            func(keyValuePair.Key, keyValuePair.Value);
        }
    }

    public static void merge<K, V>(this IDictionary<K, IList<V>> dict, IDictionary<K, IList<V>> otherDict) {
        merge<K, IList<V>, V>(dict, otherDict);
    }

    public static void merge<K, C1, V>(this IDictionary<K, C1> dict, IDictionary<K, C1> otherDict) where C1 : ICollection<V>  {
        otherDict.forEach((k, vs) => {
            if (dict.ContainsKey(k)) {
                dict[k].addAll(vs);
            } else {
                dict[k] = vs;
            }
        });
    }
}

public static class ICollectionExtensions {
    public static void addAll<T>(this ICollection<T> collection, IEnumerable<T> values) {
        if (collection is List<T> list) {
            if (values is List<T> valueList) {
                list.AddRange(valueList);
            }
        } 
        
        foreach (var obj in collection) collection.Add(obj);
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

public static class EnumerableExtensions {
    public static IEnumerable<TSource> selectNonNull<TSource>(this IEnumerable<TSource?> source) {
        return source.Where(source1 => source1 is not null).Select(source1 => source1!);
    }
}