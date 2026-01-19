using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using io.wispforest.endec;
using io.wispforest.endec.format.newtonsoft;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper.api.query;

public static class MediaQueryTypeRegistry {
    private static readonly Dictionary<Identifier, dynamic> TYPES = new ();

    public static readonly StructEndec<MediaQueryResult> RESULT_ENDEC = StructEndec.of<MediaQueryResult>(
            (ctx, serializer, instance, value) => {
                var id = value.queryTypeId;

                instance.field("id", ctx, Identifier.ENDEC, id);
            
                if (id.Equals(EmptyQueryResult.NONE)) return;
                if (!TYPES.ContainsKey(id)) {
                    throw new Exception($"Unable to encode the given Lookup Data as the given Lookup Id was not found: {id}");
                }
            
                getTypeDyn(id).encodeResult(ctx, serializer, instance, value);
            }, (ctx, deserializer, instance) => {
                var id = instance.field("id", ctx, Identifier.ENDEC);
                if (id.Equals(EmptyQueryResult.NONE)) return new EmptyQueryResult();
            
                if (!TYPES.ContainsKey(id)) {
                    throw new Exception($"Unable to decode the given Lookup Data as the given Lookup Id was not found: {id}");
                }

                return getTypeDyn(id).decodeResult(ctx, deserializer, instance);
            });
    
    public static readonly StructEndec<MediaQuery> QUERY_DATA = StructEndec.of<MediaQuery>(
            (ctx, serializer, instance, value) => {
                var id = value.queryTypeId;

                instance.field("id", ctx, Identifier.ENDEC, value.queryTypeId);
                
                if (!TYPES.ContainsKey(id)) {
                    throw new Exception($"Unable to encode the given Lookup Data as the given Lookup Id was not found: {id}");
                }
            
                getTypeDyn(id).encodeQuery(ctx, serializer, instance, value);
            }, (ctx, deserializer, instance) => {
                var id = instance.field("id", ctx, Identifier.ENDEC);
            
                if (!TYPES.ContainsKey(id)) {
                    throw new Exception($"Unable to decode the given Lookup Data as the given Lookup Id was not found: {id}");
                }

                return getTypeDyn(id).decodeQuery(ctx, deserializer, instance);
            });

    public static readonly Endec<IList<MediaQuery>> QUERY_DATA_LIST = QUERY_DATA.listOf();

    public static readonly Endec<IDictionary<Identifier, IList<MediaQuery>>> GROUPED_QUERY_DATA = QUERY_DATA_LIST.xmap(queries => {
        IDictionary<Identifier, IList<MediaQuery>> typeToQueries = new Dictionary<Identifier, IList<MediaQuery>>();

        foreach (var mediaQuery in queries) {
            typeToQueries.computeIfAbsent(mediaQuery.queryTypeId, _ => new List<MediaQuery>()).Add(mediaQuery);
        }

        return typeToQueries;
    }, dictionary => {
        return dictionary.Values.SelectMany(list => list).ToList();
    });

    public static void register<T, D, R>(T paintingLookup) where T : MediaQueryType<D, R> where R : MediaQueryResult where D : MediaQuery {
        var identifier = paintingLookup.getLookupId();
        
        if (!TYPES.TryAdd(identifier, paintingLookup)) {
            Plugin.Logger.LogError($"Unable to register the given Painting Lookup [{identifier}] due to it already being registered.");
        }
    }

    public static bool attemptToHandleQuery(MediaQuery query) {
        var id = query.queryTypeId;
        if (!TYPES.ContainsKey(id)) return false;

        var type = getTypeDyn(id);
        if (!type.canHandleQueryData(query)) return false;

        try {
            type.executeQuery(query);
        } catch (Exception e) {
            Plugin.Logger.LogError($"Unable to handle the given query type as an exception has occrued! [Id: {id}]");
            Plugin.Logger.LogError(e);

            return false;
        }
        
            
        return true;

    }

    public static dynamic getTypeDyn(Identifier identifier) {
        return TYPES[identifier];
    }
    
    public static T? getType<T, Q, R>(Identifier identifier) where T : MediaQueryType<Q, R> where R : MediaQueryResult where Q : MediaQuery {
        return identifier.Equals(EmptyQueryResult.NONE) ? null : TYPES[identifier] as T;
    }
}

public class EmptyQueryResult : MediaQueryResult {
    public static readonly Identifier NONE = Identifier.of("texture_swapper", "none");
    
    public static readonly StructEndec<EmptyQueryResult> ENDEC = endec.Endec.unit(new EmptyQueryResult());

    public static Endec<EmptyQueryResult> Endec() => ENDEC;

    public override Identifier queryTypeId => NONE;
    
    public override string ToString() => "Nothing";

    public override bool Equals(object? obj) => obj is EmptyQueryResult;

    public override int GetHashCode() => ToString().GetHashCode();
}

public class MediaQueryKey(Identifier? id = null) {

    public static readonly MediaQueryKey EMPTY = new MediaQueryKey {
        guid = Guid.Empty,
        id = EmptyQueryResult.NONE
    };

    public static readonly Endec<MediaQueryKey> ENDEC = StructEndecBuilder.of(
        GuidUtils.ENDEC.fieldOf<MediaQueryKey>("guid", s => s.guid),
        Identifier.ENDEC.optionalFieldOf<MediaQueryKey>("id", s => s.id, () => null),
        (guid, id) => new MediaQueryKey {
            id = id,
            guid = guid
        }
    );
    
    public Guid guid { get; private init; } = id?.shaToGuid() ?? Guid.NewGuid();
    
    public Identifier? id { get; private init; } = id;
    
    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return (obj is MediaQueryKey other) && guid.Equals(other.guid);
    }

    public override int GetHashCode() => guid.GetHashCode();

    public static bool operator ==(MediaQueryKey? left, MediaQueryKey? right) => Equals(left, right);

    public static bool operator !=(MediaQueryKey? left, MediaQueryKey? right) => !Equals(left, right);
}

public abstract class MediaQuery(Identifier? id = null) {

    public MediaQueryKey key { get; } = new (id);

    public abstract Identifier queryTypeId { get; }

    public virtual MediaQuery copy() {
        var data = MediaQueryTypeRegistry.QUERY_DATA.encodeFully(JsonSerializer.of, this);

        var obj = MediaQueryTypeRegistry.QUERY_DATA.decodeFully(JsonDeserializer.of, data);

        return obj;
    }
}

public abstract class MediaQueryResult {
    public Guid guid { get; internal set; }

    public abstract Identifier queryTypeId { get; }

    public virtual void addToTooltip(StringBuilder builder) { }
}

public abstract class MediaQueryType<Q, R> where R : MediaQueryResult where Q : MediaQuery {
    
    public abstract StructEndec<Q> getDataEndec();
    
    public abstract StructEndec<R> getResultEndec();
    
    public abstract Identifier getLookupId();

    public abstract void executeQuery(Q data);
    
    public SemaphoreIdentifier createSemaphoreIdentifier(bool apiQueryTask = false) {
        return new SemaphoreIdentifier(getLookupId(), maxCount: apiQueryTask ? maxCountOfAPIQueries() : maxCountOfStaticURLQueries());
    }

    public virtual int maxCountOfStaticURLQueries() => 5;
    
    public virtual int maxCountOfAPIQueries() => 2;
    
    //--
    
    public bool canHandleQueryData(MediaQuery data) => data is Q;
    
    public void executeQuery(MediaQuery data) {
        if (!canHandleQueryData(data)) {
            throw new Exception($"Unable to handle data as its not valid for this Query Type! [Type: {getLookupId()}, Data Type: {data.queryTypeId}]");
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

