using System;
using System.Collections.Generic;
using System.Globalization;
using io.wispforest;
using io.wispforest.util;
using Newtonsoft.Json.Linq;

namespace io.wispforest.textureswapper.endec.format.newtonsoft;

public class JsonDeserializer : RecursiveDeserializer<JToken>, SelfDescribedDeserializer<JToken> {
    protected JsonDeserializer(JToken serialized) : base(serialized) { }

    public static JsonDeserializer of(JToken serialized) {
        return new JsonDeserializer(serialized);
    }
    
    public SerializationContext setupContext(SerializationContext ctx) {
        return ctx.withAttributes(SerializationAttributes.HUMAN_READABLE);
    }
    
    public override byte readByte(SerializationContext ctx) {
        return readPrimitive<byte>(convertible => convertible.ToByte(CultureInfo.CurrentCulture));
    }

    public override short readShort(SerializationContext ctx) {
        return readPrimitive<short>(convertible => convertible.ToInt16(CultureInfo.CurrentCulture));
    }

    public override int readInt(SerializationContext ctx) {
        return readPrimitive<int>(convertible => convertible.ToInt32(CultureInfo.CurrentCulture));
    }

    public override long readLong(SerializationContext ctx) {
        return readPrimitive<long>(convertible => convertible.ToInt64(CultureInfo.CurrentCulture));
    }

    public override float readFloat(SerializationContext ctx) {
        return readPrimitive<float>(convertible => convertible.ToSingle(CultureInfo.CurrentCulture));
    }

    public override double readDouble(SerializationContext ctx) {
        return readPrimitive<double>(convertible => convertible.ToDouble(CultureInfo.CurrentCulture));
    }

    public override int readVarInt(SerializationContext ctx) {
        return readInt(ctx);
    }

    public override long readVarLong(SerializationContext ctx) {
        return readLong(ctx);
    }

    public override bool readBoolean(SerializationContext ctx) {
        return readPrimitive<bool>(convertible => convertible.ToBoolean(CultureInfo.CurrentCulture));
    }

    public override string readString(SerializationContext ctx) {
        return readPrimitive<string>(convertible => convertible.ToString(CultureInfo.CurrentCulture));
    }

    private T readPrimitive<T>(Func<IConvertible, T> func) where T : IConvertible {
        return readPrimitive<T>(getValue(), func);
    }
    
    private T readPrimitive<T>(JToken token, Func<IConvertible, T> func) where T : IConvertible {
        if (token is not JValue jValue) throw new Exception($"Unable to read JToken as it is not a JValue: {token}");
        if (token is null) throw new Exception($"Unable to read JToken as the given value is null and can not be converted to the needed type: {typeof(T)}");
        if (jValue.Value is not IConvertible t) throw new Exception($"Unable to read JValue as it is not a the correct type: [Type: {typeof(T)}, Obj Type: {jValue.Value}]");
        return func(t);
    }

    private T getValueSafe<T>() where T : JToken {
        var value = getValue();

        if (value is null) {
            throw new Exception("UHHHHHHHHHHHHHHHHHHHHHHHHH");
        } else if (value is not T) {
            throw new Exception($"FUCKLKDKDKDKDKDKDK: [Type: {value.GetType()}");
        }

        return value as T;
    }
    
    public override byte[] readBytes(SerializationContext ctx) {
        var array = getValueSafe<JArray>();

        var result = new byte[array.Count];
        for (int i = 0; i < array.Count; i++) {
            result[i] = readPrimitive<byte>(array[i], convertible => convertible.ToByte(CultureInfo.CurrentCulture));
        }

        return result;
    }

    public override V? readOptional<V>(SerializationContext ctx, Endec<V> endec) where V : default {
        var value = getValue();
        
        return !(value.Type.Equals(JTokenType.Null))
                ? endec.decode(ctx, this)
                : default(V);
    }

    public override SequenceDeserializer<E> sequence<E>(SerializationContext ctx, Endec<E> elementEndec) {
        return new JsonSequenceDeserializer<E>(this, ctx, elementEndec,getValueSafe<JArray>() );
    }

    public override MapDeserializer<V> map<V>(SerializationContext ctx, Endec<V> valueEndec) {
        return new JsonMapDeserializer<V>(this, ctx, valueEndec, getValueSafe<JObject>());
    }

    public override StructDeserializer structed() {
        return new JsonStructDeserializer(this, getValueSafe<JObject>());
    }

    public void readAny<S>(SerializationContext ctx, Serializer<S> visitor) where S : class {
        decodeValue(ctx, visitor, getValue());
    }
    
    private void decodeValue<S>(SerializationContext ctx, Serializer<S> visitor, JToken element) where S : class {
        var handledToken = true;
        if (element.Type.Equals(JTokenType.Null)) {
            visitor.writeOptional(ctx, JsonEndec.INSTANCE, null);
        } else if (element is JValue primitive) {
            if (primitive.Value is string str) {
                visitor.writeString(ctx, str);
            } else if (primitive.Value is bool bl) {
                visitor.writeBoolean(ctx, bl);
            } else if (primitive.Value is IConvertible value) {
                try {
                    var asLong = value.ToInt64(CultureInfo.CurrentCulture);

                    if ((byte) asLong == asLong) {
                        visitor.writeByte(ctx, value.ToByte(CultureInfo.CurrentCulture));
                    } else if ((short) asLong == asLong) {
                        visitor.writeShort(ctx, value.ToInt16(CultureInfo.CurrentCulture));
                    } else if ((int) asLong == asLong) {
                        visitor.writeInt(ctx, value.ToInt32(CultureInfo.CurrentCulture));
                    } else {
                        visitor.writeLong(ctx, asLong);
                    }
                } catch (Exception bruh) when (bruh is FormatException || bruh is OverflowException) {
                    var asDouble = value.ToDouble(CultureInfo.CurrentCulture);

                    if ((float) asDouble == asDouble) {
                        visitor.writeFloat(ctx, value.ToSingle(CultureInfo.CurrentCulture));
                    } else {
                        visitor.writeDouble(ctx, asDouble);
                    }
                }
            } else {
                handledToken = false;
            }
        } else if (element is JArray array) {
            using (var sequence = visitor.sequence(ctx, EndecUtils.of<JToken>(this.decodeValue, (_, _) => null), array.Count)) {
                foreach (var jToken in array) {
                    sequence.element(jToken);
                }
            }
        } else if (element is JObject obj) {
            using (var map = visitor.map(ctx, EndecUtils.of<JToken>(this.decodeValue, (_, _) => null), obj.Count)) {
                foreach (var entry in obj) {
                    map.entry(entry.Key, entry.Value);
                }
            }
        } else {
            handledToken = false;
            
        }
        
        if (!handledToken) {
            throw new Exception($"Non-standard, unrecognized JsonElement implementation cannot be decoded: {element}");
        }
    }
    
    private class JsonSequenceDeserializer<V> : SequenceDeserializer<V> {

        private readonly JsonDeserializer deserializer;
        private readonly SerializationContext ctx;
        private readonly Endec<V> valueEndec;
        private readonly IEnumerator<JToken> elements;
        private readonly int size;

        public JsonSequenceDeserializer(JsonDeserializer deserializer, SerializationContext ctx, Endec<V> valueEndec, JArray elements) {
            this.deserializer = deserializer;
        
            this.ctx = ctx;
            this.valueEndec = valueEndec;

            this.elements = elements.GetEnumerator();
            this.size = elements.Count;
        }

        public int estimatedSize() {
            return this.size;
        }

        public bool MoveNext() {
            return this.elements.MoveNext();
        }

        public V Current => next();
    
        public V next() {
            var element = this.elements.Current;
            return deserializer.frame(
                    () => element,
                    () => this.valueEndec.decode(this.ctx, deserializer)
            );
        }

        public void Reset() {
            this.elements.Reset();
        }

        public void Dispose() {
            this.elements.Dispose();
        }
    }
    
    private class JsonMapDeserializer<V> : MapDeserializer<V> {

        private readonly JsonDeserializer deserializer;
        private readonly SerializationContext ctx;
        private readonly Endec<V> valueEndec;
        private readonly IEnumerator<KeyValuePair<String, JToken>> entries;
        private readonly int size;

        public JsonMapDeserializer(JsonDeserializer deserializer, SerializationContext ctx, Endec<V> valueEndec, JObject entries) {
            this.deserializer = deserializer;
            this.ctx = ctx;
            this.valueEndec = valueEndec;

            this.entries = entries.GetEnumerator();
            this.size = entries.Count;
        }

        public int estimatedSize() {
            return this.size;
        }

        public bool MoveNext() {
            return this.entries.MoveNext();
        }

        public KeyValuePair<string, V> Current => next();
    
        public KeyValuePair<string, V> next() {
            var entry = this.entries.Current;
            return deserializer.frame(
                    () => entry.Value,
                    () => new KeyValuePair<string, V>(entry.Key, this.valueEndec.decode(this.ctx, deserializer))
            );
        }

        public void Reset() {
            this.entries.Reset();
        }

        public void Dispose() {
            this.entries.Dispose();
        }
    }
    
    private class JsonStructDeserializer : StructDeserializer {

        private readonly JsonDeserializer deserializer;
        private readonly JObject obj;

        public JsonStructDeserializer(JsonDeserializer deserializer, JObject obj) {
            this.deserializer = deserializer;
            this.obj = obj;
        }

        public F? field<F>(string name, SerializationContext ctx, Endec<F> endec, Func<F>? defaultValueFactory) {
            var element = this.obj[name];
            if (element is null) {
                if(defaultValueFactory is null) {
                    throw new Exception($"Field '{name}' was missing from serialized data, but no default value was provided");
                }

                return defaultValueFactory();
            }

            try {
                return deserializer.frame(() => element, () => endec.decode(ctx, deserializer));
            }
            catch (Exception e) {
                Plugin.logIfDebugging(source => source.LogError(e));
                Plugin.logIfDebugging(source => source.LogError(obj.ToString()));
                throw e;
            }
        }
    }
}