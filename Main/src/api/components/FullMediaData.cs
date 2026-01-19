using System;
using io.wispforest.endec;
using io.wispforest.endec.impl;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper.api.components;

public class FullMediaData(MediaQueryKey key, Identifier id, MediaInfo info, MediaQueryResult result) : EndecGetter<FullMediaData> {

    public static readonly StructEndec<FullMediaData> ENDEC = StructEndecBuilder.of(
        MediaQueryKey.ENDEC.fieldOf<FullMediaData>("key", s => s.key),
        Identifier.ENDEC.fieldOf<FullMediaData>("id", s => s.id),
        MediaInfo.ENDEC.fieldOf<FullMediaData>("info", s => s.info), 
        MediaQueryTypeRegistry.RESULT_ENDEC.fieldOf<FullMediaData>("result", s => s.result),
        (key, id, info, result) => new (key, id, info, result));

    public static Endec<FullMediaData> Endec() => ENDEC;
    
    public MediaQueryKey key { get; } = key;
    public Identifier id { get; } = id;
    public MediaInfo info { get; } = info;
    public MediaQueryResult result { get; } = result;

    public bool isError() => this.info.isError;

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is FullMediaData other && id.Equals(other.id);
    }

    public override int GetHashCode() => id.GetHashCode();
}