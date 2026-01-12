using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using io.wispforest.endec;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.utils;
using UnityEngine;
using Object = System.Object;

namespace io.wispforest.textureswapper.api.target;

public static class TargetRegistry {
    private static readonly Dictionary<Identifier, dynamic> endecs = new ();

    public static readonly StructEndec<Target> TARGET_ENDEC = StructEndec.of<Target>(
            (ctx, serializer, instance, target) => {
                var id = target.type;

                instance.field("id", ctx, Identifier.ENDEC, id);
                
                if (!endecs.ContainsKey(id)) {
                    throw new Exception($"Unable to encode the given Lookup Data as the given Lookup Id was not found: {id}");
                }
            
                getEndec(id).encodeResult(ctx, serializer, instance, target);
            }, (ctx, deserializer, instance) => {
                var id = instance.field("id", ctx, Identifier.ENDEC);
            
                if (!endecs.ContainsKey(id)) {
                    throw new Exception($"Unable to decode the given Lookup Data as the given Lookup Id was not found: {id}");
                }

                return getEndec(id).decodeResult(ctx, deserializer, instance);
            });

    public static Endec<T> register<T>(Identifier id, Endec<T> endec) where T : Target {
        if (!endecs.TryAdd(id, endec)) {
            Plugin.Logger.LogError($"Unable to register the given Painting Lookup [{id}] due to it already being registered.");
        }

        return endec;
    }

    public static dynamic getEndec(Identifier identifier) {
        return endecs[identifier];
    }
}

public interface Target {
    public Identifier type { get; }
}

public interface BaseObjectTarget : Target  {
    Predicate<UnityEngine.Object> objPredicate { get; }

    bool isValid(UnityEngine.Object obj);
}

public interface BaseObjectTarget<in T> : BaseObjectTarget where T : UnityEngine.Object {
    new Predicate<T> objPredicate { get; }

    bool BaseObjectTarget.isValid(UnityEngine.Object obj) => obj is T;
}

public interface ObjectTarget: BaseObjectTarget<UnityEngine.Object> {
    abstract Predicate<UnityEngine.Object> BaseObjectTarget<UnityEngine.Object>.objPredicate { get; }
}

public interface GameObjectTarget : BaseObjectTarget<GameObject> {
    abstract Predicate<GameObject> BaseObjectTarget<GameObject>.objPredicate { get; }
}

public interface MeshRendererTarget : BaseObjectTarget<MeshRenderer> {
    abstract Predicate<MeshRenderer> BaseObjectTarget<MeshRenderer>.objPredicate { get; }
}

public interface MaterialTarget : Target {
    Predicate2<int, Material> materialPredicate { get; }
}

public class CompoundTarget (IList<Target> targets) : Target {

    public static readonly Identifier TYPE = Identifier.of(Plugin.id, "compound");
    public static readonly Endec<CompoundTarget> ENDEC = TargetRegistry.register(
            TYPE,
        StructEndecBuilder.of(
            TargetRegistry.TARGET_ENDEC.listOf().fieldOf<CompoundTarget>("targets", s => s.targets),
            (targets) => new CompoundTarget(targets)
        )
    );
    
    public IList<Target> targets { get; } = targets;
    
    public Identifier type => TYPE;
}

//--

public class RegexObjectTarget(string targetType, RegexRawData regexData) : BaseObjectTarget {

    public static readonly Identifier TYPE = Identifier.of(Plugin.id, "regex_name");
    
    public static readonly Endec<RegexObjectTarget> ENDEC = TargetRegistry.register(
            TYPE,
            StructEndecBuilder.of(
                    endec.Endec.STRING.optionalFieldOf<RegexObjectTarget>("target_type", s => s.targetType, () => "game_object"),
                    RegexUtils.RAW_DATA_ENDEC.flatFieldOf<RegexObjectTarget>(s => s.regexData),
                    (targetType, data) => new RegexObjectTarget(targetType, data)
            )
    );
    
    private string targetType { get; } = targetType;
    private RegexRawData regexData { get; } = regexData;
    private Regex regex { get; } = regexData.toRegex();

    public Predicate<UnityEngine.Object> objPredicate => o => regex.IsMatch(o.name);
    
    public bool isValid(UnityEngine.Object obj) {
        return targetType switch {
            "game_object" => obj is GameObject,
            "mesh_renderer" => obj is MeshRenderer,
            "material" => obj is Material,
            "level" => obj is Level,
            _ => false
        };
    }

    public Identifier type => TYPE;
}

public class TreeTraversalTarget(Target target, string direction, int amount) : Target {

    public Target target { get; } = target;
    public string direction { get; } = direction;
    public int amount { get; } = amount;
    
    public static readonly Identifier TYPE = Identifier.of(Plugin.id, "tree_traversal");
    
    public static readonly Endec<TreeTraversalTarget> ENDEC = TargetRegistry.register(
            TYPE,
            StructEndecBuilder.of(
                    TargetRegistry.TARGET_ENDEC.fieldOf<TreeTraversalTarget>("target", s => s.target),
                    endec.Endec.STRING.optionalFieldOf<TreeTraversalTarget>("direction", s => s.direction, () => "up"),
                    endec.Endec.INT.optionalFieldOf<TreeTraversalTarget>("amount", s => s.amount, () => 1),
                    (target, data, amount) => new TreeTraversalTarget(target, data, amount)
            )
    );
    
    public Identifier type => TYPE;
}

public static class TargetHandling {
    public static bool isObjectATarget(IList<Target> targets, Level level, GameObject obj, MeshRenderer renderer, int index, Material material) {
        foreach (var target in targets) {
            if (!isObjectATarget(target, level, obj, renderer, index, material)) {
                return false;
            }
        }

        return true;
    }
    
    public static bool isObjectATarget(Target target, Level level, GameObject obj, MeshRenderer renderer, int index, Material material) {
        if (target is BaseObjectTarget baseObjTarget) {
            if (baseObjTarget.isValid(obj) && !baseObjTarget.objPredicate(obj)) {
                return false;
            }
                
            if (baseObjTarget.isValid(renderer) && !baseObjTarget.objPredicate(renderer)) {
                return false;
            }
                
            if (baseObjTarget.isValid(material) && !baseObjTarget.objPredicate(material)) {
                return false;
            }
        } else if (target is MaterialTarget materialTarget) {
            if (!materialTarget.materialPredicate(index, material)) {
                return false;
            }
        } else if (target is CompoundTarget compoundTarget) {
            if (!isObjectATarget(compoundTarget.targets, level, obj, renderer, index, material)) {
                return false;
            }
        } else if (target is TreeTraversalTarget traversalTarget) {
            var lookThoughChildren = traversalTarget.direction is "down" or "d";

            if (!lookThoughChildren) {
                if (!isObjectATarget(traversalTarget.target, level, obj.getParent(traversalTarget.amount), renderer, index, material)) {
                    return false;
                }
            } else {    
                // TODO: ADD ABILITY TO LOOK THOUGH CHILDREN
            }
        }

        return true;
    }
}

public delegate bool Predicate2<in T1, in T2>(T1 obj1, T2 obj2);