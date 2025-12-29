using System;
using System.Collections.Generic;
using System.Linq;
using io.wispforest.impl;
using io.wispforest.util;

namespace io.wispforest.textureswapper.endec.format.edm;

public class EdmElement<T> {

    private T value { get; }
    private EdmElementType type { get; }

    internal EdmElement(T value, EdmElementType type) {
        this.value = value;
        this.type = type;
    }
    
    public V cast<V>() {
        if (value is V v) return v;

        throw new InvalidCastException($"Unable to cast [{value?.GetType()}] to [{typeof(V)}");
    }

    public EdmElementType getElementType => type;
    
    public Object unwrap() {
        if (this.value is List<object> list) {
            return list.Select(o => (o as EdmElement<object>).unwrap()).ToList();
        } else if (this.value is Dictionary<string, object> map) {
            var dict = new Dictionary<string, object>();
            
            foreach (var entry in map) {
                dict[entry.Key] = (entry.Value as EdmElement<object>).unwrap();
            }

            return dict;
        } else if (Nullable.GetUnderlyingType(value.GetType()) != null) {
            return value is not null 
                    ? (value! as EdmElement<object>).unwrap() 
                    : null;
        } else {
            return this.value;
        }
    }

    /**
     * Create a copy of this EDM element as an {@link EdmMap}, which
     * : the {@link io.wispforest.endec.util.MapCarrier} interface
     */
    public EdmMap asMap() {
        if(this.type != EdmElementType.MAP) {
            throw new InvalidCastException("Cannot cast EDM element of type " + this.type + " to MAP");
        }
        
        return new EdmMap(new Dictionary<string, EdmElement<object>>(this.cast<Dictionary<string, EdmElement<object>>>()));
    }

    public override bool Equals(object? obj) {
        if (this == obj) return true;
        if (!(obj is EdmElement<object> that)) return false;
        if ((this.value is null && that.value is null) || (this.value?.Equals(that.value) ?? false)) {
            return this.type == that.type;
        }

        return false;
    }

    public override int GetHashCode() {
        int result = this.value.GetHashCode();
        result = 31 * result + this.type.GetHashCode();
        return result;
    }

    // public override string ToString() {
    //     return format(new BlockWriter()).buildResult();
    // }

    // protected BlockWriter format(BlockWriter formatter) {
    //     return switch (this.type){
    //         case BYTES -> {
    //             yield formatter.writeBlock("bytes(", ")", false, blockWriter -> {
    //                 blockWriter.write(Arrays.toString(Base64.getEncoder().encode(this.<byte[]>cast())));
    //             });
    //         }
    //         case MAP -> {
    //             yield formatter.writeBlock("map({", "})", blockWriter -> {
    //                 var map = this.<Map<String, EdmElement<?>>>cast();
    //
    //                 int idx = 0;
    //
    //                 for (var entry : map.entrySet()) {
    //                     formatter.write("\"" + entry.getKey() + "\": ");
    //                     entry.getValue().format(formatter);
    //
    //                     if (idx < map.size() - 1) formatter.writeln(",");
    //
    //                     idx++;
    //                 }
    //             });
    //         }
    //         case SEQUENCE -> {
    //             yield formatter.writeBlock("sequence([", "])", blockWriter -> {
    //                 var list = this.<List<EdmElement<?>>>cast();
    //
    //                 for (int idx = 0; idx < list.size(); idx++) {
    //                     list.get(idx).format(formatter);
    //                     if (idx < list.size() - 1) formatter.writeln(",");
    //                 }
    //             });
    //         }
    //         case OPTIONAL -> {
    //             yield formatter.writeBlock("optional(", ")", false, blockWriter -> {
    //                 var optional = this.<Optional<EdmElement<?>>>cast();
    //
    //                 optional.ifPresentOrElse(
    //                         edmElement -> edmElement.format(formatter),
    //                         () -> formatter.write(""));
    //             });
    //         }
    //         case STRING -> {
    //             yield formatter.writeBlock("string(\"", "\")", false, blockWriter -> {
    //                 blockWriter.write(Objects.toString(value));
    //             });
    //         }
    //         default -> {
    //             yield formatter.writeBlock(type.formatName() + "(", ")", false, blockWriter -> {
    //                 blockWriter.write(Objects.toString(value));
    //             });
    //         }
    //     };
    //}

    
    // TODO: IMPL
    // public static EdmElement<Dictionary<string, EdmElement<object>>> consumeMap(Dictionary<string, EdmElement<object>> value) {
    //     return new EdmElement<Dictionary<string, EdmElement<object>>>(value, Type.MAP); // Hangry
    // }
}

public class EdmElements {
    
    public static readonly EdmElement<EdmElement<object>?> EMPTY = new (null, EdmElementType.OPTIONAL);
    
    public static EdmElement<sbyte> i8(sbyte value) {
        return new EdmElement<sbyte>(value, EdmElementType.I8);
    }

    public static EdmElement<byte> u8(byte value) {
        return new EdmElement<byte>(value, EdmElementType.U8);
    }

    public static EdmElement<short> i16(short value) {
        return new EdmElement<short>(value, EdmElementType.I16);
    }

    public static EdmElement<ushort> u16(ushort value) {
        return new EdmElement<ushort>(value, EdmElementType.U16);
    }

    public static EdmElement<int> i32(int value) {
        return new EdmElement<int>(value, EdmElementType.I32);
    }

    public static EdmElement<uint> u32(uint value) {
        return new EdmElement<uint>(value, EdmElementType.U32);
    }

    public static EdmElement<long> i64(long value) {
        return new EdmElement<long>(value, EdmElementType.I64);
    }

    public static EdmElement<ulong> u64(ulong value) {
        return new EdmElement<ulong>(value, EdmElementType.U64);
    }

    public static EdmElement<float> f32(float value) {
        return new EdmElement<float>(value, EdmElementType.F32);
    }

    public static EdmElement<double> f64(double value) {
        return new EdmElement<double>(value, EdmElementType.F64);
    }

    public static EdmElement<bool> @bool(bool value) {
        return new EdmElement<bool>(value, EdmElementType.BOOLEAN);
    }

    public static EdmElement<string> @string(string value) {
        return new EdmElement<string>(value, EdmElementType.STRING);
    }

    public static EdmElement<byte[]> bytes(byte[] value) {
        return new EdmElement<byte[]>(value, EdmElementType.BYTES);
    }

    public static EdmElement<EdmElement<object>?> optional(EdmElement<object>? value) {
        if(value is null) return EdmElements.EMPTY;
        
        return new EdmElement<EdmElement<object>?>(value, EdmElementType.OPTIONAL);
    }
    public static EdmElement<IList<EdmElement<object>>> sequence(IList<EdmElement<object>> value) {
        return new EdmElement<IList<EdmElement<object>>>(value.ToList(), EdmElementType.SEQUENCE);
    }

    public static EdmElement<IDictionary<string, EdmElement<object>>> map(IDictionary<string, EdmElement<object>> value) {
        return new EdmElement<IDictionary<string, EdmElement<object>>>(value, EdmElementType.MAP);
    }
    
    internal static EdmElement<object> cast<V>(object value) {
        if (value is EdmElement<object> element) return element;

        throw new InvalidCastException($"Unable to cast [{value?.GetType()}] to [{typeof(V)}");
    }
}
        
public enum EdmElementType {
    I8,
    U8,
    I16,
    U16,
    I32,
    U32,
    I64,
    U64,
    F32,
    F64,

    BOOLEAN,
    STRING,
    BYTES,
    OPTIONAL,

    SEQUENCE,
    MAP
}

public static class EdmElementTypeUtils {
    public static String formatName(this EdmElementType type){
        return Enum.GetName(typeof(EdmElementType), type)!.ToLower();
    }
}

public class EdmMap : EdmElement<Dictionary<string, EdmElement<object>>>, MapCarrier {

    private readonly Dictionary<string, EdmElement<object>> map;

    internal EdmMap(Dictionary<string, EdmElement<object>> map) : base(map, EdmElementType.MAP) {
        this.map = map;
    }
    
    public T getWithErrors<T>(SerializationContext ctx, KeyedEndec<T> key) {
        if (!this.has(key)) return key.defaultValue();
        return key.endec.decodeFully(ctx, EdmDeserializer.of, this.map[key.key]);
    }
            
    public void put<T>(SerializationContext ctx, KeyedEndec<T> key, T value) {
        //this.map[key.key] = key.endec.encodeFully(ctx, EdmSerializer.of, value);
    }
            
    public void delete<T>(KeyedEndec<T> key) {
        this.map.Remove(key.key);
    }
            
    public bool has<T>(KeyedEndec<T> key) {
        return this.map.ContainsKey(key.key);
    }
}