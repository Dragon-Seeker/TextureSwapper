using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Sirenix.Utilities;
using Unity.VisualScripting;
using UnityEngine;

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

        foreach (var obj in values) collection.Add(obj);
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

    public static bool isNotEmpty<T>(this IEnumerable<T> source) {
        return !source.isEmpty();
    }

    public static bool isEmpty<T>(this IEnumerable<T> source) {
        if (source is ICollection<T> collection) return collection.Count == 0;

        return (source as IEnumerable).isEmpty();
    }

    public static bool isNotEmpty(this IEnumerable source) {
        return !source.isEmpty();
    }

    public static bool isEmpty(this IEnumerable source) {
        if (source is ICollection collection) return collection.Count == 0;
        
        var enumerator = source.GetEnumerator();

        var value = enumerator.MoveNext();

        if (enumerator is IDisposable disposable) disposable.Dispose();

        return value;
    }
}

public static class TransformExtensions {
    public static IEnumerable<Transform> children(this Transform transform) {
        return transform.Cast<Transform>();
    }

    public static IEnumerable<GameObject> childrenObjects(this Transform transform) {
        return transform.Cast<Transform>().Select(t => t.gameObject);
    }

    public static void unpackChildren(this GameObject gameObject, Action<GameObject> entryHandler, Action<Action> nestScope) {
        entryHandler(gameObject);

        foreach (var childObj in gameObject.transform.childrenObjects()) nestScope(() => childObj.unpackChildren(entryHandler, nestScope));
    }


    public static void unpackChildren(this GameObject gameObject, Action<GameObject> entryHandler) {
        entryHandler(gameObject);
        
        foreach(var childObj in gameObject.transform.childrenObjects()) childObj.unpackChildren(entryHandler);
    }
    
    public static IEnumerable<GameObject> getAllChildrenObjects(this GameObject gameObject) {
        var children = new List<GameObject>();
        
        gameObject.unpackChildren(o => children.Add(o));
        
        return children;
    }

    public static string dumpNameTree(this GameObject gameObject, string indent = "  ", string indentSuffix = "") {
        var builder = new StringBuilder();
        
        var indentLevel = new Stack<string>([indentSuffix]);
        
        gameObject.unpackChildren((o) => builder.Append(indentLevel.Peek()).Append(o.name).AppendLine(), 
            action => { 
                indentLevel.Push(indent + indentLevel.Peek());
            
                action();

                indentLevel.Pop();
            }
        );

        return builder.ToString();
    }
}