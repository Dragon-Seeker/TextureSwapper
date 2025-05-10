using System;
using io.wispforest;
using io.wispforest.util;
using Newtonsoft.Json.Linq;

namespace io.wispforest.textureswapper.endec.format.newtonsoft;

public class JsonSerializer : RecursiveSerializer<JToken>, SelfDescribedSerializer<JToken> {

    internal JToken? prefix;

    protected JsonSerializer(JToken prefix) : base(null) {
        this.prefix = prefix;
    }

    public static JsonSerializer of() {
        return new JsonSerializer(null);
    }
    
    public SerializationContext setupContext(SerializationContext ctx) {
        return ctx.withAttributes(SerializationAttributes.HUMAN_READABLE);
    }
    
    //--

    public override void writeByte(SerializationContext ctx, byte value) {
        consume(new JValue(value));
    }

    public override void writeShort(SerializationContext ctx, short value) {
        consume(new JValue(value));
    }

    public override void writeInt(SerializationContext ctx, int value) {
        consume(new JValue(value));
    }

    public override void writeLong(SerializationContext ctx, long value) {
        consume(new JValue(value));
    }

    public override void writeFloat(SerializationContext ctx, float value) {
        consume(new JValue(value));
    }

    public override void writeDouble(SerializationContext ctx, double value) {
        consume(new JValue(value));
    }

    public override void writeVarInt(SerializationContext ctx, int value) {
        consume(new JValue(value));
    }

    public override void writeVarLong(SerializationContext ctx, long value) {
        consume(new JValue(value));
    }

    public override void writeBoolean(SerializationContext ctx, bool value) {
        consume(new JValue(value));
    }

    public override void writeString(SerializationContext ctx, string value) {
        consume(new JValue(value));
    }

    public override void writeBytes(SerializationContext ctx, byte[] bytes) {
        var result = new JArray();
        for (int i = 0; i < bytes.Length; i++) {
            result.Add(new JValue(bytes[i]));
        }

        consume(result);
    }

    public override void writeOptional<V>(SerializationContext ctx, Endec<V> endec, V? optional) where V : default {
        if (optional is null) {
            consume(JValue.CreateNull());
        } else {
            endec.encode(ctx, this, optional!);
        }
    }

    public override SequenceSerializer<E> sequence<E>(SerializationContext ctx, Endec<E> elementEndec, int size) {
        return new JsonSequenceSerializer<E>(this, ctx, elementEndec, size);
    }

    public override MapSerializer<V> map<V>(SerializationContext ctx, Endec<V> valueEndec, int size) {
        return new JsonMapSerializer<V>(this, ctx, valueEndec);
    }

    public override StructSerializer structed() {
        return new JsonStructSerializer(this);
    }
    
    //--
    private class JsonStructSerializer : StructSerializer {
    
        private readonly JsonSerializer _serializer;
        private readonly JObject _result;
        
        public JsonStructSerializer(JsonSerializer serializer) {
            _serializer = serializer;
            
            if (serializer.prefix is not null) {
                if (serializer.prefix is JObject prefixObject) {
                    _result = prefixObject;
                    serializer.prefix = null;
                } else {
                    throw new Exception($"Incompatible prefix of type used {this._serializer.GetType().Name} for JSON map/struct");
                }
            } else {
                _result = new JObject();
            }
        }
        
        public StructSerializer field<F>(string name, SerializationContext ctx, Endec<F> endec, F value, bool mayOmit) {
            _serializer.frame(encoded => {
                endec.encode(ctx, _serializer, value);

                var element = encoded.require("struct field");
                
                if (mayOmit && element.Type.Equals(JTokenType.Null)) return;

                _result.Add(name, element);
            });

            return this;
        }
        
        public void end() {
            _serializer.consume(_result);
        }
    }

    private class JsonMapSerializer<V> : MapSerializer<V> {

        private readonly JsonSerializer _serializer;
        private readonly SerializationContext _ctx;
        private readonly Endec<V> _valueEndec;
        private readonly JObject _result;

        public JsonMapSerializer(JsonSerializer serializer, SerializationContext ctx, Endec<V> valueEndec) {
            _serializer = serializer;
            _ctx = ctx;
            _valueEndec = valueEndec;
            
            if (serializer.prefix is not null) {
                if (serializer.prefix is JObject prefixObject) {
                    _result = prefixObject;
                    serializer.prefix = null;
                } else {
                    throw new Exception($"Incompatible prefix of type used {_serializer.GetType().Name} for JSON map/struct");
                }
            } else {
                _result = new JObject();
            }
        }

        
        public void entry(string key, V value) {
            _serializer.frame(encoded => {
                _valueEndec.encode(_ctx, _serializer, value);
                _result.Add(key, encoded.require("map value"));
            });
        }
        
        public void end() {
            _serializer.consume(_result);
        }
    }

    private class JsonSequenceSerializer<V> : SequenceSerializer<V> {

        private readonly JsonSerializer _serializer;
        private readonly SerializationContext _ctx;
        private readonly Endec<V> _valueEndec;
        private readonly JArray _result;

        public JsonSequenceSerializer(JsonSerializer serializer, SerializationContext ctx, Endec<V> valueEndec, int size) {
            _serializer = serializer;
            _ctx = ctx;
            _valueEndec = valueEndec;

            if (serializer.prefix != null) {
                if (serializer.prefix is JArray prefixArray) {
                    _result = prefixArray;
                    serializer.prefix = null;
                } else {
                    throw new Exception($"Incompatible prefix of type {serializer.prefix.GetType().Name} used for JSON sequence");
                }
            } else {
                _result = new JArray();
            }
        }

        public void element(V element) {
            _serializer.frame(encoded => {
                _valueEndec.encode(_ctx, _serializer, element);
                _result.Add(encoded.require("sequence element"));
            });
        }

        public void end() {
            _serializer.consume(_result);
        }
    }
}

