using System;
using io.wispforest.impl;
using io.wispforest.textureswapper.api.query;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper.api.components;

public class FullMediaData : EndecGetter<FullMediaData> {

    public static readonly StructEndec<FullMediaData> ENDEC = StructEndecBuilder.of(
            Identifier.ENDEC.fieldOf<FullMediaData>("id", s => s.id),
            MediaInfo.ENDEC.fieldOf<FullMediaData>("info", s => s.info), 
            MediaQueryTypeRegistry.RESULT_ENDEC.fieldOf<FullMediaData>("result", s => s.result),
            (id, info, result) => new FullMediaData(id, info, result));

    public static Endec<FullMediaData> Endec() {
        return ENDEC;
    }
    
    public Identifier id { get; }
    public MediaInfo info { get; }
    public MediaQueryResult result { get; }

    public FullMediaData(Identifier id, MediaInfo info, MediaQueryResult result) {
        this.id = id;
        this.info = info;
        this.result = result;
    }

    public bool isError() {
        return this.info.isError;
    }

    protected bool Equals(FullMediaData other) {
        return id.Equals(other.id)/* && info.Equals(other.info) && result.Equals(other.result)*/;
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((FullMediaData)obj);
    }

    public override int GetHashCode() {
        return id.GetHashCode(); //HashCode.Combine(id, info, result);
    }
}