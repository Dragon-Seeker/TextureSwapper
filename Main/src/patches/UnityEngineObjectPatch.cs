using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using io.wispforest.textureswapper.utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace io.wispforest.textureswapper.patches;

[HarmonyPatch(typeof(Object))]
public class UnityEngineObjectPatch {
    
    // [HarmonyPatch(nameof(Object.Instantiate), typeof(Object))] // <T> and Object
    // [HarmonyPatch(nameof(Object.Instantiate), typeof(Object), typeof(Scene))]
    // [HarmonyPatch(nameof(Object.Instantiate), typeof(Object), typeof(InstantiateParameters))] // <T>
    // [HarmonyPatch(nameof(Object.Instantiate), typeof(Object), typeof(Vector3), typeof(Quaternion))]
    // [HarmonyPatch(nameof(Object.Instantiate), typeof(Object), typeof(Vector3), typeof(Quaternion), typeof(Transform))]
    // [HarmonyPatch(nameof(Object.Instantiate), typeof(Object), typeof(Vector3), typeof(Quaternion), typeof(InstantiateParameters))] // <T>
    // [HarmonyPatch(nameof(Object.Instantiate), typeof(Object), typeof(Transform), typeof(bool))]
    
    static IEnumerable<MethodBase> TargetMethods() {
        var allMethods = typeof(Object).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Instantiate");

        foreach (var method in allMethods) {
            var parameters = method.GetParameters();
            var length = parameters.Length;

            var genericTypes = new [] { typeof(Object) /*typeof(GameObject), typeof(MeshRenderer)*/ };
            
            if (length == 1) {
                if (!method.IsGenericMethodDefinition) {
                    yield return method;
                } else {
                    foreach (var genericType in genericTypes) yield return method.MakeGenericMethod(genericType);
                }
            } else if (length == 2) {
                if (!method.IsGenericMethodDefinition) {
                    if (parameters.isMatch(typeof(Object), typeof(Scene))) yield return method;
                } else {
                    if(parameters.isMatch(1, typeof(InstantiateParameters))) foreach (var genericType in genericTypes) yield return method.MakeGenericMethod(genericType);
                }
            } else if (length == 3) {
                if (!method.IsGenericMethodDefinition) {
                    if(parameters.isMatch(typeof(Object), typeof(Vector3), typeof(Quaternion))) yield return method;
                    if(parameters.isMatch(typeof(Object), typeof(Transform), typeof(bool))) yield return method;
                } 
            } else if (length == 4) {
                if (!method.IsGenericMethodDefinition ) {
                    if(parameters.isMatch(typeof(Object), typeof(Vector3), typeof(Quaternion), typeof(Transform))) yield return method;
                } else {
                    if(parameters.isMatch(typeof(Object), typeof(Vector3), typeof(Quaternion), typeof(InstantiateParameters))) foreach (var genericType in genericTypes) yield return method.MakeGenericMethod(genericType);
                }
            }
        }
    }
    
    [HarmonyPostfix]
    public static void afterObjectInstantiation(Object __result) {
        ObjectInstantiateEvent.setupAndRun(__result);
    }
}

static class ParameterInfosExt {
    public static bool isMatch(this ParameterInfo[] array, params Type[] pattern) {
        return array.isMatch(0, pattern);
    }

    public static bool isMatch(this ParameterInfo[] array, int offset = 0, params Type[] pattern) {
        if (array.Length < (pattern.Length + offset)) return false;
        
        for (var i = 0; i < pattern.Length; i++) {
            var info = array[offset + i];
            var type = pattern[i];

            if (info.GetType() != type) return false;
        }

        return true;
    }
}