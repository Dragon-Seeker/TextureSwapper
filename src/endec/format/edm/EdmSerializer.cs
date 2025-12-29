// using System;
// using System.Collections.Generic;
// using io.wispforest.util;
//
// namespace io.wispforest.textureswapper.endec.format.edm;
//
// public class EdmSerializer : RecursiveSerializer<EdmElement<dynamic>>, SelfDescribedSerializer<EdmElement<object>> {
//
//     protected EdmSerializer() : base(null) { }
//
//     public static EdmSerializer of() {
//         return new EdmSerializer();
//     }
//
//     // ---
//     
//     public override void writeByte(SerializationContext ctx, byte value) {
//         this.consume(EdmElements.i8(unchecked((sbyte) value)));
//     }
//
//     public override void writeShort(SerializationContext ctx, short value) {
//         this.consume(EdmElements.i16(value));
//     }
//
//     public override void writeInt(SerializationContext ctx, int value) {
//         this.consume(EdmElements.i32(value));
//     }
//
//     public override void writeLong(SerializationContext ctx, long value) {
//         this.consume(EdmElements.i64(value));
//     }
//
//     // ---
//
//     public override void writeFloat(SerializationContext ctx, float value) {
//         this.consume(EdmElements.f32(value));
//     }
//
//     public override void writeDouble(SerializationContext ctx, double value) {
//         this.consume(EdmElements.f64(value));
//     }
//
//     // ---
//
//     public override void writeVarInt(SerializationContext ctx, int value) {
//         this.consume(EdmElements.i32(value));
//     }
//
//     public override void writeVarLong(SerializationContext ctx, long value) {
//         this.consume(EdmElements.i64(value));
//     }
//
//     // ---
//
//     public override void writeBoolean(SerializationContext ctx, bool value) {
//         this.consume(EdmElements.@bool(value));
//     }
//
//     public override void writeString(SerializationContext ctx, String value) {
//         this.consume(EdmElements.@string(value));
//     }
//
//     public override void writeBytes(SerializationContext ctx, byte[] bytes) {
//         this.consume(EdmElements.bytes(bytes));
//     }
//
//     public override void writeOptional<V>(SerializationContext ctx, Endec<V> endec, V? optional) where V : default  {
//         var result = new EdmElement<object>[1];
//         this.frame(encoded => {
//             if (optional is not null) endec.encode(ctx, this, optional);
//             result[0] = encoded.value();
//         });
//
//         this.consume(EdmElements.optional(result[0]));
//     }
//
//     // ---
//
//     public override SequenceSerializer<E> sequence<E>(SerializationContext ctx, Endec<E> elementEndec, int size) {
//         return new Sequence<E>(this, elementEndec, ctx);
//     }
//
//     public override MapSerializer<V> map<V>(SerializationContext ctx, Endec<V> valueEndec, int size) {
//         return new Map<V>(this, valueEndec, ctx);
//     }
//
//     public override StructSerializer structed() {
//         return new Struct(this);
//     }
//
//     // ---
//
//     internal class Sequence<V> : SequenceSerializer<V> {
//
//         private readonly EdmSerializer serializer;
//         private readonly Endec<V> elementEndec;
//         private readonly SerializationContext ctx;
//
//         private readonly IList<EdmElement<object>> result;
//
//         public Sequence(EdmSerializer serializer, Endec<V> elementEndec, SerializationContext ctx) {
//             this.elementEndec = elementEndec;
//             this.ctx = ctx;
//             this.result = new List<EdmElement<object>>();
//         }
//
//         public void element(V element) {
//             serializer.frame(encoded => {
//                 this.elementEndec.encode(ctx, serializer, element);
//                 this.result.Add(encoded.require("sequence element"));
//             });
//         }
//
//         public void end() {
//             serializer.consume(EdmElements.sequence(this.result));
//         }
//     }
//
//     internal class Map<V> : MapSerializer<V> {
//
//         private readonly EdmSerializer serializer;
//         private readonly Endec<V> valueEndec;
//         private readonly SerializationContext ctx;
//
//         private readonly IDictionary<string, EdmElement<object>> result;
//
//         public Map(EdmSerializer serializer, Endec<V> valueEndec, SerializationContext ctx) {
//             this.serializer = serializer;
//             this.valueEndec = valueEndec;
//             this.ctx = ctx;
//
//             this.result = new Dictionary<string, EdmElement<object>>();
//         }
//         
//         public void entry(String key, V value) {
//             serializer.frame(encoded => {
//                 this.valueEndec.encode(ctx, serializer, value);
//                 this.result[key] = encoded.require("map value");
//             });
//         }
//         
//         public void end() {
//             serializer.consume(EdmElements.map(this.result));
//         }
//     }
//
//     internal class Struct : StructSerializer {
//
//         private readonly EdmSerializer serializer;
//         private readonly IDictionary<string, EdmElement<object>> result;
//
//         public Struct(EdmSerializer serializer) {
//             this.serializer = serializer;
//             this.result = new Dictionary<string, EdmElement<object>>();
//         }
//
//         public StructSerializer field<F>(String name, SerializationContext ctx, Endec<F> endec, F value, bool mayOmit) {
//             serializer.frame(encoded => {
//                 endec.encode(ctx, serializer, value);
//
//                 var element = encoded.require("struct field");
//
//                 if (mayOmit && element.Equals(EdmElements.EMPTY)) return;
//
//                 this.result[name] = element;
//             });
//
//             return this;
//         }
//         
//         public void end() {
//             serializer.consume(EdmElements.map(this.result));
//         }
//     }
// }