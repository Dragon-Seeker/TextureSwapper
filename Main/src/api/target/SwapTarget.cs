using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using io.wispforest.endec;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.utils;
using UnityEngine;

namespace io.wispforest.textureswapper.api.target;

public static class SwapTargetRegistry {
    private static readonly Dictionary<Identifier, dynamic> endecs = new ();

    public static readonly StructEndec<SwapTarget> TARGET_ENDEC = StructEndec.of<SwapTarget>(
            (ctx, serializer, instance, target) => {
                var id = target.type;

                instance.field("type", ctx, Identifier.ENDEC, id);
                
                if (!endecs.ContainsKey(id)) {
                    throw new Exception($"Unable to encode the given Lookup Data as the given Lookup Id was not found: {id}");
                }
                
                getEndec(id).encodeStruct(ctx, serializer, instance, target);
            }, (ctx, deserializer, instance) => {
                var id = instance.field("type", ctx, Identifier.ENDEC);
            
                if (id == null || !endecs.ContainsKey(id)) {
                    throw new Exception($"Unable to decode the given Lookup Data as the given Lookup Id was not found: {id}");
                }

                return getEndec(id).decodeStruct(ctx, deserializer, instance);
            });

    public static StructEndec<T> register<T>(Identifier id, StructEndec<T> endec) where T : SwapTarget {
        if (!endecs.TryAdd(id, endec)) {
            Plugin.Logger.LogError($"Unable to register the given Painting Lookup [{id}] due to it already being registered.");
        }

        return endec;
    }

    public static dynamic getEndec(Identifier identifier) => endecs[identifier];

    static SwapTargetRegistry() {
        init();
    }

    public static void init() {
        register(RegexObjectSwapTarget.TYPE, RegexObjectSwapTarget.ENDEC);
        register(TreeTraversalSwapTarget.TYPE, TreeTraversalSwapTarget.ENDEC);
        register(CompoundSwapTarget.TYPE, CompoundSwapTarget.ENDEC);
        register(RegexMaterialSwapTarget.TYPE, RegexMaterialSwapTarget.ENDEC);
    }
}

public class TargetInstance(Identifier? queryId, SwapTarget target) {

    public static readonly StructEndec<TargetInstance> ENDEC = StructEndecBuilder.of(
        Identifier.ENDEC.optionalFieldOf<TargetInstance>("query_id", s => s.queryId, () => null),
        SwapTargetRegistry.TARGET_ENDEC.flatFieldOf<TargetInstance>(s => s.target),
        (id, swapTarget) => new TargetInstance(id, swapTarget) 
    );
    
    public Identifier? queryId { get; } = queryId;
    public SwapTarget target { get; } = target;
}

public interface SwapTarget {
    public Identifier type { get; }
}

public interface BaseObjectSwapTarget : SwapTarget  {
    Predicate<UnityEngine.Object> objPredicate { get; }

    bool isValid(UnityEngine.Object obj);
}

public interface BaseObjectSwapTarget<in T> : BaseObjectSwapTarget where T : UnityEngine.Object {
    new Predicate<T> objPredicate { get; }

    bool BaseObjectSwapTarget.isValid(UnityEngine.Object obj) => obj is T;
}

public interface ObjectSwapTarget: BaseObjectSwapTarget<UnityEngine.Object> {
    abstract Predicate<UnityEngine.Object> BaseObjectSwapTarget<UnityEngine.Object>.objPredicate { get; }
}

public interface GameObjectSwapTarget : BaseObjectSwapTarget<GameObject> {
    abstract Predicate<GameObject> BaseObjectSwapTarget<GameObject>.objPredicate { get; }
}

public interface MeshRendererSwapTarget : BaseObjectSwapTarget<MeshRenderer> {
    abstract Predicate<MeshRenderer> BaseObjectSwapTarget<MeshRenderer>.objPredicate { get; }
}

public interface MaterialSwapTarget : SwapTarget {
    Predicate2<int, Material> materialPredicate { get; }
}

public class CompoundSwapTarget (IList<SwapTarget> targets) : SwapTarget {

    public static readonly Identifier TYPE = Identifier.of(Plugin.id, "compound");
    public static readonly StructEndec<CompoundSwapTarget> ENDEC = StructEndecBuilder.of(
        SwapTargetRegistry.TARGET_ENDEC.listOf().fieldOf<CompoundSwapTarget>("targets", s => s.targets),
        (targets) => new CompoundSwapTarget(targets)
    );
    
    public IList<SwapTarget> targets { get; } = targets;
    
    public Identifier type => TYPE;
}

//--

public class RegexMaterialSwapTarget(RegexRawData nameData, RegexRawData? indexData) : MaterialSwapTarget {
    public static readonly Identifier TYPE = Identifier.of(Plugin.id, "regex_material");
    
    private RegexRawData? indexData { get; } = indexData;
    private Regex? indexRegex { get; } = indexData?.toRegex();
    
    private RegexRawData nameData { get; } = nameData;
    private Regex nameRegex { get; } = nameData.toRegex();
    
    public static readonly StructEndec<RegexMaterialSwapTarget> ENDEC = StructEndecBuilder.of(
        RegexUtils.RAW_DATA_ENDEC.fieldOf<RegexMaterialSwapTarget>("name_regex", s => s.nameData),
        RegexUtils.RAW_DATA_ENDEC.optionalFieldOf<RegexMaterialSwapTarget>("index_regex", s => s.indexData, () => null),
        (nameData, indexData) => new RegexMaterialSwapTarget(nameData, indexData)
    );

    public Predicate2<int, Material> materialPredicate => (index, material) => {
        if (indexRegex != null && !indexRegex.IsMatch(index.ToString())) return false;

        return nameRegex.IsMatch(material.name);
    };

    public Identifier type => TYPE;
}

public class RegexObjectSwapTarget(string targetType, RegexRawData regexData) : BaseObjectSwapTarget {

    public static readonly Identifier TYPE = Identifier.of(Plugin.id, "regex_name");
    
    public static readonly StructEndec<RegexObjectSwapTarget> ENDEC = StructEndecBuilder.of(
        endec.Endec.STRING.optionalFieldOf<RegexObjectSwapTarget>("target_type", s => s.targetType, () => "game_object"),
        RegexUtils.RAW_DATA_ENDEC.fieldOf<RegexObjectSwapTarget>("name_regex", s => s.regexData),
        (targetType, data) => new RegexObjectSwapTarget(targetType, data)
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

public class TreeTraversalSwapTarget(SwapTarget target, string direction, int amount) : SwapTarget {

    public SwapTarget target { get; } = target;
    public string direction { get; } = direction;
    public int amount { get; } = amount;
    
    public static readonly Identifier TYPE = Identifier.of(Plugin.id, "tree_traversal");
    
    public static readonly StructEndec<TreeTraversalSwapTarget> ENDEC = StructEndecBuilder.of(
        SwapTargetRegistry.TARGET_ENDEC.fieldOf<TreeTraversalSwapTarget>("target", s => s.target),
        endec.Endec.STRING.optionalFieldOf<TreeTraversalSwapTarget>("direction", s => s.direction, () => "up"),
        endec.Endec.INT.optionalFieldOf<TreeTraversalSwapTarget>("amount", s => s.amount, () => 1),
        (target, data, amount) => new TreeTraversalSwapTarget(target, data, amount)
    );
    
    public Identifier type => TYPE;
}

public static class TargetHandling {
    public static bool isObjectATarget(ICollection<TargetInstance> targets, Level level, GameObject obj, MeshRenderer renderer, int index, Material material, out ICollection<Identifier> queryIds) {
        queryIds = new List<Identifier>();

        var bl = false;
        
        foreach (var instance in targets) {
            if (!isObjectATarget(instance, level, obj, renderer, index, material, out var id)) continue;
            
            if (id != null) queryIds.Add(id);

            if (!bl) bl = true;
        }

        return bl;
    }

    public static bool isObjectATarget(TargetInstance instance, Level level, GameObject obj, MeshRenderer renderer, int index, Material material, out Identifier? id) {
        var bl = isObjectATarget(instance.target, level, obj, renderer, index, material);

        id = instance.queryId;

        return bl;
    }

    public static bool isObjectATarget(SwapTarget target, Level level, GameObject obj, MeshRenderer renderer, int index, Material material) {
        if (target is BaseObjectSwapTarget baseObjTarget) {
            if (baseObjTarget.isValid(obj) && !baseObjTarget.objPredicate(obj)) {
                return false;
            }
                
            if (baseObjTarget.isValid(renderer) && !baseObjTarget.objPredicate(renderer)) {
                return false;
            }
                
            if (baseObjTarget.isValid(material) && !baseObjTarget.objPredicate(material)) {
                return false;
            }
        } else if (target is MaterialSwapTarget materialTarget) {
            if (!materialTarget.materialPredicate(index, material)) {
                return false;
            }
        } else if (target is CompoundSwapTarget compoundTarget) {
            if (!compoundTarget.targets.All(swapTarget => isObjectATarget(swapTarget, level, obj, renderer, index, material))) {
                return false;
            }
        } else if (target is TreeTraversalSwapTarget traversalTarget) {
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