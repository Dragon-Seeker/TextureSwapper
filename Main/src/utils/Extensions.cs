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
using Object = UnityEngine.Object;

namespace io.wispforest.textureswapper.utils;

public delegate void EntryHandler<T>(T t);
    
public delegate void EntryUnpacker<T>(T t, List<T> list);
public delegate bool EntryIgnorer<in T>(T t);
public delegate string EntryNameGetter<T>(T t);
public delegate void EntryNestScopeCallback(Action action);

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
        foreach (var keyValuePair in dict) func(keyValuePair.Key, keyValuePair.Value);
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
        if (collection is List<T> list && values is List<T> valueList) {
            list.AddRange(valueList);
        }

        foreach (var obj in values) collection.Add(obj);
    }
}

public static class IListExtensions {
    public static IList<T> getSublistSafe<T>(this IList<T> list, int startInclusiveIndex, int endInclusiveIndex = -1) {
        return list.Count > 0 ? list.getSublist(startInclusiveIndex, endInclusiveIndex) : new List<T>();
    }

    public static IList<T> getSublist<T>(this IList<T> list, int startInclusiveIndex, int endInclusiveIndex = -1) {
        if (endInclusiveIndex <= -1) endInclusiveIndex = list.Count - 1;
        
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
        
        return right is not null 
                ? func(right) 
                : throw new NullReferenceException("Unable to handle Either based tuple method due to the Left and Right values are both Null");
    }
}

public static class EnumerableUtils {
    
    private class DelegatingEnumerable<T>(Func<IEnumerator<T>> enumeratorMaker) : IEnumerable<T> {
        public IEnumerator<T> GetEnumerator() => enumeratorMaker();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    
    public static IEnumerable<T> delegating<T>(Func<IEnumerator<T>> enumeratorMaker) => new DelegatingEnumerable<T>(enumeratorMaker);
    
    public static IEnumerable<T> Concat<T>(this IEnumerable<T> first, Func<IEnumerator<T>> enumeratorMaker) => first.Concat(delegating(enumeratorMaker));
    
    public static IEnumerable<T> selectNonNull<T>(this IEnumerable<T?> source) => source.Where(source1 => source1 is not null).Select(source1 => source1!);

    public static bool isNotEmpty<T>(this IEnumerable<T> source) => !source.isEmpty();

    public static bool isEmpty<T>(this IEnumerable<T> source) => (source is ICollection<T> c) ? c.Count == 0 : (source as IEnumerable).isEmpty();

    public static bool isNotEmpty(this IEnumerable source) => !source.isEmpty();

    public static bool isEmpty(this IEnumerable source) {
        if (source is ICollection collection) return collection.Count == 0;
        
        var enumerator = source.GetEnumerator();

        var value = enumerator.MoveNext();

        if (enumerator is IDisposable disposable) disposable.Dispose();

        return value;
    }

    public static string toPrettyString(this IEnumerable source) => $"[{string.Join(",", source)}]";
}

public static class TransformExtensions {
    
    public static bool hasChild(this Transform transform, string name, bool resetLocals = true) {
        return transform.Find(name)?.gameObject != null;
    }
    
    public static GameObject? getChild(this Transform transform, string name, bool resetLocals = true) {
        return transform.Find(name)?.gameObject;
    }
    
    public static GameObject getOrAddChild(this Transform transform, string name, bool resetLocals = true) {
        return transform.Find(name)?.gameObject 
               ?? transform.addChild(name, resetLocals);
    }

    public static GameObject addChild(this Transform transform, string name, bool resetLocals = true) {
        var obj = new GameObject(name);
        
        obj.transform.SetParent(transform);

        if (resetLocals) {
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
        }

        return obj;
    }
    
    public static IEnumerable<Transform> children(this Transform transform) {
        return transform.Cast<Transform>();
    }

    public static IEnumerable<GameObject> childrenObjects(this Transform transform) {
        return transform.Cast<Transform>().Select(t => t.gameObject);
    }
    
    public static Transform getParent(this Transform transform, int unpackAmount = 1) {
        int level = 0;

        if (unpackAmount == 0) return transform;
        
        if (unpackAmount < 0) {
            throw new Exception($"Can not get parent unpack for '{unpackAmount}' as it must be 1 or more.");
        }

        while (level < unpackAmount) {
            var parentTransform = transform.parent;

            if (parentTransform == null) break;
            
            transform = parentTransform;
            
            level++;
        }

        return transform;
    }
}

public static class GameObjectExtensions {
    
    public static GameObject getParent(this GameObject gameObject, int unpackAmount = 1) {
        return gameObject.transform.getParent(unpackAmount).gameObject;
    }
    
    public static bool hasChild(this GameObject gameObject, string name, bool resetLocals = true) {
        return gameObject.transform.hasChild(name, resetLocals);
    }
    
    public static GameObject getOrAddChild(this GameObject gameObject, string name, bool resetLocals = true) {
        return gameObject.transform.getOrAddChild(name, resetLocals);
    }
    
    public static GameObject? getChild(this GameObject gameObject, string name, bool resetLocals = true) {
        return gameObject.transform.getChild(name, resetLocals);
    }
    
    public static GameObject addChild(this GameObject gameObject, string name, bool resetLocals = true) {
        return gameObject.transform.addChild(name, resetLocals);
    }
    
    public static IEnumerable<GameObject> unpackGameObject(this GameObject gameObject) {
        return new[] { gameObject }.Concat(gameObject.transform.childrenObjects().SelectMany(o => o.unpackGameObject()));
    }
    
    public static string dumpDebugInfoTree(this GameObject gameObject, string indent = "  ", string indentSuffix = "", 
            EntryUnpacker<System.Object>? entryUnpack = null, EntryIgnorer<System.Object>? ignorer = null, EntryNameGetter<System.Object>? getter = null) {
        var builder = new StringBuilder();
        
        var indentLevel = new Stack<string>([indentSuffix]);
        
        gameObject.unpackChildrenCSharpObjects(
                (o) => { 
                    if (ignorer?.Invoke(o) ?? false) return;

                    String? name = getter?.Invoke(o);

                    if (name == null && o is Object obj) {
                        name = obj.name;
                    }
                    
                    builder.Append(indentLevel.Peek()).Append($"{name} : {o.GetType().Name}").AppendLine();
                }, 
                entryUnpack ?? ((_, _) => { }), 
                action => { 
                    indentLevel.Push(indent + indentLevel.Peek());
                
                    action();

                    indentLevel.Pop(); 
                });

        return builder.ToString();
    }
}

public static class ObjectUtils {
    public static void unpackChildrenCSharpObjects(this System.Object obj, EntryHandler<System.Object> entryHandler, EntryUnpacker<System.Object>? unpacker, EntryNestScopeCallback nestScope) {
        entryHandler(obj);

        var objects = new List<System.Object>();

        if (obj is GameObject gameObject) {
            objects.addAll(gameObject.GetComponents(typeof(Component)));
            objects.addAll(gameObject.transform.childrenObjects());
            objects.RemoveAll(o => o is Transform);
        }

        unpacker?.Invoke(obj, objects);

        foreach (var childObject in objects) {
            if (childObject == null) continue;
            nestScope(() => childObject.unpackChildrenCSharpObjects(entryHandler, unpacker, nestScope));
        }
    }
}

public static class UnityObjectExtensions {
    public static void unpackChildrenObjects(this Object @object, EntryHandler<Object> handler) {
        handler(@object);

        var objects = new List<Object>();

        if (@object is GameObject gameObject) {
            objects.addAll(gameObject.GetComponents(typeof(Component)));
            objects.addAll(gameObject.transform.childrenObjects());
            objects.RemoveAll(o => o is Transform);
        }

        foreach (var childObject in objects) {
            if (childObject == null) continue;
            childObject.unpackChildrenObjects(handler);
        }
    }
    
    public static void unpackChildrenObjects(this Object @object, EntryHandler<Object> handler, EntryUnpacker<Object> entryUnpack, EntryNestScopeCallback callback) {
        handler(@object);

        var objects = new List<Object>();

        if (@object is GameObject gameObject) {
            objects.addAll(gameObject.GetComponents(typeof(Component)));
            objects.addAll(gameObject.transform.childrenObjects());
            objects.RemoveAll(o => o is Transform);
        }

        entryUnpack(@object, objects);

        foreach (var childObject in objects) {
            if (childObject == null) continue;
            callback(() => childObject.unpackChildrenObjects(handler, entryUnpack, callback));
        }
    }
}

public static class ComponentExtensions {
    public static Transform getParent(this Component component, int unpackAmount = 1) {
        return component.transform.getParent(unpackAmount);
    }
}
