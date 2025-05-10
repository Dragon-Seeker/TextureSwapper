using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ExitGames.Client.Photon;
using ExitGames.Client.Photon.StructWrapping;
using io.wispforest.format.binary;
using Photon.Pun;
using BinaryReaderDeserializer = io.wispforest.format.binary.BinaryReaderDeserializer;

namespace io.wispforest.textureswapper;

public class PhotonEndecAddon {

    private static bool hasTypeBeenRegistered = false;
    
    internal static void init() {
        if (hasTypeBeenRegistered) return;
        
        var addedProperly = PhotonPeer.RegisterType(typeof(ByteDataAccess), 201, obj => {
            if (obj is not ByteDataAccess holder) {
                throw new Exception($"Object was found not to be a ByteDataHolder: [Type: {obj.GetType()}, Obj:{obj}]");
            }
            
            var bytes = holder.getBytes();
            
            //Plugin.logIfDebugging(() => $"ENCODING THE GIVEN ENDEC ByteDataAccess: {Buffer.ByteLength(bytes)}");
        
            return bytes;
        }, bytes => {
            //Plugin.logIfDebugging(() => $"DECODING THE GIVEN ENDEC ByteDataAccess: {Buffer.ByteLength(bytes)}");
            
            return new ByteDataHolder().setBytes(bytes);
        });
        
        if (addedProperly) {
            Plugin.Logger.LogWarning("Was able to add onto Photon Peer!");
        } else {
            Plugin.Logger.LogError("Wasn't able to add onto Photon Peer, FUCKKKKKK!");
        }
        
        hasTypeBeenRegistered = true;
    }

    static PhotonEndecAddon() {
        init();
    }
}

public static class PhotonStreamExtensions {
    
    static PhotonStreamExtensions() {
        PhotonEndecAddon.init();
    }
    
    public static PhotonStreamOperation handleObject<T>(this PhotonStream stream, Func<T> encodeGetter, Action<T> decodeHandler) where T : EndecGetter<T> {
        if (stream.IsWriting) {
            addObject(stream, encodeGetter());
            
            return PhotonStreamOperation.ENCODE;
        } else {
            decodeHandler(getObject<T>(stream));
            
            return PhotonStreamOperation.DECODE;
        }
    }
    
    public static PhotonStreamOperation handleObject<T>(this PhotonStream stream, ref T t) where T : EndecGetter<T> {
        if (stream.IsWriting) {
            addObject(stream, t);
            
            return PhotonStreamOperation.ENCODE;
        } else {
            t = getObject<T>(stream);
            
            return PhotonStreamOperation.DECODE;
        }
    }
    
    public static void addObject<T>(this PhotonStream stream, T data) where T : EndecGetter<T> {
        if (!stream.IsWriting) {
            throw new Exception($"You can not write a object to a PhotonStream that is currently reading!");
        }
        
        stream.SendNext(new EndecedStructWrapper<T>(data));
    }

    public static T getObject<T>(this PhotonStream stream) where T : EndecGetter<T> {
        if (!stream.IsReading) {
            throw new Exception($"You can not read an object from a PhotonStream that is currently writting!");
        }

        var obj = stream.PeekNext();

        if (obj is ByteDataHolder bytes) {
            stream.ReceiveNext();
            
            return ByteDataAccess.decodeFromBytes(EndecGetter.Endec<T>(), bytes.getBytes());
        }
        
        throw new Exception($"Unable to use the current peeked object from a PhotonStream due to the type mismatch! [Type: {obj.GetType()}, Required Type: PhotonEndecHolder]");
    }
    
    public static void dumpPhotonStreamToLog(this PhotonStream stream, Action<Action<Action<string>>> logActionCallback) {
        var streamType = stream.GetType();

        var info = streamType.GetField("currentItem", BindingFlags.Instance);
        
        var streamObjs = new List<object>();
        var currentIndex = (info?.GetValue(stream) as int?) ?? -1;

        streamObjs.AddRange(stream.ToArray());
        
        logActionCallback(logAction => logAction("Dumping the given PhotonStream Data: "));
        
        for (var i = 0; i < streamObjs.Count; i++) {
            var obj = streamObjs[i];

            var type = obj.GetType();

            logActionCallback(logAction => logAction($"    [{i}, {(currentIndex == i ? "X" : "O")}]: Type=({type.FullName}), Object:({obj})"));
        }
    }

}


public enum PhotonStreamOperation {
    ENCODE = 0,
    DECODE = 1
}

public class ByteDataHolder : ByteDataAccess {

    private byte[] bytes = new byte[0];
    
    public ByteDataAccess setBytes(byte[] bytes) {
        this.bytes = bytes;

        return this;
    }

    public byte[] getBytes() {
        return bytes;
    }

    public override bool Equals(object? obj) {
        if (this == obj) return true;
        if (this is ByteDataAccess access) {
            return this.getBytes().Equals(access.getBytes());
        }
        
        return false;
    }

    public override int GetHashCode() {
        return this.bytes.GetHashCode();
    }
}

public interface ByteDataAccess {
    public byte[] getBytes();
    
    public static byte[] encodeAsBytes<T>(Endec<T> endec, T data) {
        var stream = new MemoryStream();
        
        endec.encodeFully(() => BinaryWriterSerializer.of(new BinaryWriter(stream)), data);
            
        return stream.GetBuffer();
    }
    
    public static T decodeFromBytes<T>(Endec<T> endec, byte[] bytes) {
        return endec.decodeFully((reader) => BinaryReaderDeserializer.of(reader), new BinaryReader(new MemoryStream(bytes)));
    }
}

internal sealed class EndecedStructWrapper<T> : StructWrapper, ByteDataAccess where T : EndecGetter<T> {
    public readonly T data;

    public EndecedStructWrapper(T data) : base(typeof(ByteDataAccess), WrappedType.Unknown){
        this.data = data;
    }

    public static EndecedStructWrapper<T> of<T>(object obj) where T : EndecGetter<T> {
        if (obj is not T t) throw new Exception($"Unable to create EndecedStructWrapper as the object is not the correct type! [Type: {typeof(object)}, Obj: {obj}]");
        
        return new EndecedStructWrapper<T>(t);
    }

    public byte[] getBytes() {
        return ByteDataAccess.encodeAsBytes(EndecGetter.Endec<T>(), data);
    }

    public override bool Equals(object? obj) {
        if (this == obj) return true;
        
        if (obj is EndecedStructWrapper<T> otherWrapper) {
            return this.data.Equals(otherWrapper.data);
        }

        return false;
    }

    public override int GetHashCode() {
        return this.data.GetHashCode();
    }

    public override object Box() {
        return data;
    }

    public override void DisconnectFromPool() { /* NO OP */ }
    public override void Dispose() { /* NO OP */ }

    public override string ToString() => this.Unwrap<T>().ToString();

    public override string ToString(bool writeTypeInfo) {
        return writeTypeInfo ? $"(EndecedStructWrapper<{wrappedType}>){this.Unwrap<T>().ToString()}" : ToString();
    }
}