using io.wispforest;
using Newtonsoft.Json.Linq;

namespace io.wispforest.textureswapper.endec.format.newtonsoft;

public class JsonEndec : Endec<JToken> {
    public static readonly JsonEndec INSTANCE = new ();

    private JsonEndec() {}
    
    public override void encode<E>(SerializationContext ctx, Serializer<E> serializer, JToken value) {
        if (serializer is SelfDescribedSerializer<E>) {
            JsonDeserializer.of(value).readAny(ctx, serializer);
            return;
        }
        
        serializer.writeString(ctx, JsonUtils.writeToString(value));
    }
    
    public override JToken decode<E>(SerializationContext ctx, Deserializer<E> deserializer) {
        if (deserializer is SelfDescribedDeserializer<E> selfDescribedDeserializer) {
            var json = JsonSerializer.of();
            selfDescribedDeserializer.readAny(ctx, json);

            return json.result();
        }
        
        return JsonUtils.readFromString(deserializer.readString(ctx));
    }
}