using System;
using System.Collections.Generic;
using io.wispforest.textureswapper.utils;
using io.wispforest.util;

namespace io.wispforest.textureswapper.api.query;

public static class MediaQueryTypeRegistry {
    private static readonly Dictionary<Identifier, dynamic> TYPES = new ();

    public static readonly StructEndec<MediaQueryResult> RESULT_ENDEC = StructEndecUtils.of<MediaQueryResult>(
            (ctx, serializer, instance, value) => {
                var id = value.getQueryTypeId();

                instance.field("id", ctx, Identifier.ENDEC, value.getQueryTypeId());
            
                if (id.Equals(EmptyQueryResult.NONE)) return;
                if (!TYPES.ContainsKey(id)) {
                    throw new Exception($"Unable to encode the given Lookup Data as the given Lookup Id was not found: {id}");
                }
            
                TYPES[id].encodeResult(ctx, serializer, instance, value);
            }, (ctx, deserializer, instance) => {
                var id = instance.field("id", ctx, Identifier.ENDEC);
                if (id.Equals(EmptyQueryResult.NONE)) return new EmptyQueryResult();
            
                if (!TYPES.ContainsKey(id)) {
                    throw new Exception($"Unable to decode the given Lookup Data as the given Lookup Id was not found: {id}");
                }

                return TYPES[id].decodeResult(ctx, deserializer, instance);
            });
    
    public static readonly StructEndec<MediaQuery> QUERY_DATA = StructEndecUtils.of<MediaQuery>(
            (ctx, serializer, instance, value) => {
                var id = value.getQueryTypeId();

                instance.field("id", ctx, Identifier.ENDEC, value.getQueryTypeId());
                
                if (!TYPES.ContainsKey(id)) {
                    throw new Exception($"Unable to encode the given Lookup Data as the given Lookup Id was not found: {id}");
                }
            
                TYPES[id].encodeQuery(ctx, serializer, instance, value);
            }, (ctx, deserializer, instance) => {
                var id = instance.field("id", ctx, Identifier.ENDEC);
            
                if (!TYPES.ContainsKey(id)) {
                    throw new Exception($"Unable to decode the given Lookup Data as the given Lookup Id was not found: {id}");
                }

                return TYPES[id].decodeQuery(ctx, deserializer, instance);
            });

    public static void register<T, D, R>(T paintingLookup) where T : MediaQueryType<D, R> where R : MediaQueryResult where D : MediaQuery {
        var identifier = paintingLookup.getLookupId();
        
        if (!TYPES.TryAdd(identifier, paintingLookup)) {
            Plugin.Logger.LogError($"Unable to register the given Painting Lookup [{identifier}] due to it already being registered.");
        }
    }

    public static bool attemptToHandleQuery(MediaQuery query) {
        var id = query.getQueryTypeId();
        if (!TYPES.ContainsKey(id)) return false;

        var type = TYPES[id];
        if (!type.canHandleQueryData(query)) return false;
        
        type.executeQuery(query);
            
        return true;

    }

    public static T? getType<T, D, R>(Identifier identifier) where T : MediaQueryType<D, R> where R : MediaQueryResult where D : MediaQuery {
        return identifier.Equals(EmptyQueryResult.NONE) ? null : TYPES[identifier];
    }
}

public class EmptyQueryResult : MediaQueryResult {
    public static readonly Identifier NONE = Identifier.of("texture_swapper", "none");
    
    public static readonly StructEndec<EmptyQueryResult> ENDEC = EndecUtils.unit(new EmptyQueryResult());

    public static Endec<EmptyQueryResult> Endec() => ENDEC;

    public override Identifier getQueryTypeId() => NONE;
    
    public override string ToString() => "Nothing";

    public override bool Equals(object? obj) => obj is EmptyQueryResult;

    public override int GetHashCode() => ToString().GetHashCode();
}

public abstract class MediaQuery {
    public abstract Identifier getQueryTypeId();
}

public abstract class MediaQueryResult {
    public abstract Identifier getQueryTypeId();
}

public abstract class MediaQueryType<Q, R> where R : MediaQueryResult where Q : MediaQuery {
    
    public abstract StructEndec<Q> getDataEndec();
    
    public abstract StructEndec<R> getResultEndec();
    
    public abstract Identifier getLookupId();

    public abstract void executeQuery(Q data);
    
    public SemaphoreIdentifier createSemaphoreIdentifier() {
        return new SemaphoreIdentifier(getLookupId(), maxCount: 3);
    }

    public virtual int maxCountOfConcurrentTypes() => 3;
    
    //--
    
    public bool canHandleQueryData(MediaQuery data) => data is Q;
    
    public void executeQuery(MediaQuery data) {
        if (!canHandleQueryData(data)) {
            throw new Exception($"Unable to handle data as its not valid for this Query Type! [Type: {getLookupId()}, Data Type: {data.getQueryTypeId()}]");
        }
        
        executeQuery((Q) data);
    }
    
    public Q decodeQuery(SerializationContext ctx, Deserializer<dynamic> serializer, StructDeserializer instance) {
        return getDataEndec().decodeStruct(ctx, serializer, instance);
    }
    
    public void encodeQuery(SerializationContext ctx, Serializer<dynamic> serializer, StructSerializer instance, MediaQuery query) {
        if (query is not Q q) throw new InvalidCastException($"Unable to encode the given query object [{query}] for the given type: {getLookupId()}");
        
        getDataEndec().encodeStruct(ctx, serializer, instance, q);
    }
    
    public R decodeResult(SerializationContext ctx, Deserializer<dynamic> serializer, StructDeserializer instance) {
        return getResultEndec().decodeStruct(ctx, serializer, instance);
    }
    
    public void encodeResult(SerializationContext ctx, Serializer<dynamic> serializer, StructSerializer instance, MediaQueryResult result) {
        if (result is not R r) throw new InvalidCastException($"Unable to encode the given result object [{result}] for the given type: {getLookupId()}");
        
        getResultEndec().encodeStruct(ctx, serializer, instance, r);
    }
}

//--

