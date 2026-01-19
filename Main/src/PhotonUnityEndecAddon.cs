using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ExitGames.Client.Photon;
using ExitGames.Client.Photon.StructWrapping;
using io.wispforest.endec;
using io.wispforest.endec.format.binary;
using Photon.Pun;

namespace io.wispforest.textureswapper;

public static class PhotonUnityEndecAddon {

    private static bool hasTypeBeenRegistered = false;

    private static (bool addedProperly, string message)? initMessage;
    
    internal static void init(Action<(bool addedProperly, string message)>? logCallback = null) {
        if (initMessage != null && logCallback != null) logCallback(initMessage.Value);
        if (hasTypeBeenRegistered) return;
        
        var addedProperly = PhotonPeer.RegisterType(typeof(ByteDataAccess), 201, obj => {
            return obj is ByteDataAccess holder
                ? holder.bytes
                : throw new Exception($"Object was found not to be a ByteDataHolder: [Type: {obj.GetType()}, Obj:{obj}]");
        }, ByteDataAccess.of);

        var msg = (
            addedProperly,
            addedProperly
                ? "PhotonEndecAddon was able to add the needed CustomType to Protocol"
                : "PhotonEndecAddon was unable to add the REQUIRED CustomType to Protocol, things will go wrong!"
        );
        
        (logCallback ?? (message => initMessage = message))(msg);
        
        hasTypeBeenRegistered = true;
    }

    static PhotonUnityEndecAddon() => init();
}

public static class PhotonStreamExtensions {
    
    static PhotonStreamExtensions() => PhotonUnityEndecAddon.init();
    
    public static StreamOperation handleObject<T>(this PhotonStream stream, ref T t) where T : EndecGetter<T> => stream.handleObject(EndecGetter.Endec<T>(), ref t);
    
    public static StreamOperation handleObject<T>(this PhotonStream stream, Endec<T> endec, ref T t) {
        if (stream.IsWriting) {
            stream.addObject(endec, t);
            
            return StreamOperation.ENCODE;
        } else {
            t = stream.getObject(endec);
            
            return StreamOperation.DECODE;
        }
    }

    public static void handleObject<T>(this PhotonStream stream, Func<T> onEncode, Action<T> onDecode) where T : EndecGetter<T> => stream.handleObject(EndecGetter.Endec<T>(), onEncode, onDecode);
    
    public static void handleObject<T>(this PhotonStream stream, Endec<T> endec, Func<T> onEncode, Action<T> onDecode) {
        if (stream.IsWriting) {
            stream.addObject(endec, onEncode());
        } else {
            onDecode(stream.getObject(endec));
        }
    }
    
    public static void addObject<T>(this PhotonStream stream, Endec<T> endec, T data)  {
        if (!stream.IsWriting) throw new Exception("Unable to write from PhotonStream as its currently Write Only!");
        
        stream.SendNext(ByteDataAccess.of(data, endec));
    }

    public static T getObject<T>(this PhotonStream stream, Endec<T> endec)  {
        if (!stream.IsReading) throw new Exception("Unable to read from PhotonStream as its currently Read Only!");

        var obj = stream.PeekNext();

        if (obj is not ByteDataAccess bytes) {
            throw new Exception($"Unable to use the current peeked object from a PhotonStream due to the type mismatch! [Type: {obj.GetType()}, Required Type: PhotonEndecHolder]");
        }
        
        stream.ReceiveNext();
        
        return ByteDataAccess.decodeFromBytes(endec, bytes.bytes);
    }
    
    public static void dumpStreamContents(this PhotonStream stream, Action<string> logCallback) {
        var info = stream.GetType().GetField("currentItem", BindingFlags.Instance);
        
        var currentIndex = (info?.GetValue(stream) as int?) ?? -1;
        var streamObjs = new List<object>(stream.ToArray());
        
        logCallback("Dumping the given PhotonStream Data: ");
        
        for (var i = 0; i < streamObjs.Count; i++) {
            var obj = streamObjs[i];

            logCallback($"    [{i}, {(currentIndex == i ? "X" : "O")}]: Type=({obj.GetType().FullName}), Object:({obj})");
        }
    }
}

public enum StreamOperation {
    ENCODE = 0,
    DECODE = 1
}

public interface ByteDataAccess {
    public byte[] bytes { get; }

    public static ByteDataAccess of(byte[] bytes) => new ByteDataHolder { bytes = bytes };
    
    public static ByteDataAccess of<T>(object obj, Endec<T> endec) {
        return obj is T t 
            ? new EndecedStructWrapper<T>(t, endec)
            : throw new Exception($"Unable to create EndecedStructWrapper as the object is not the correct type! [Type: {typeof(object)}, Obj: {obj}]");
    }
    
    public static ByteDataAccess ofGetter<T>(object obj) where T : EndecGetter<T> => of(obj, EndecGetter.Endec<T>());

    public static byte[] encodeAsBytes<T>(Endec<T> endec, T data) {
        var stream = new MemoryStream();
        
        endec.encodeFully(() => BinaryWriterSerializer.of(new BinaryWriter(stream)), data);
            
        return stream.GetBuffer();
    }
    
    public static T decodeFromBytes<T>(Endec<T> endec, byte[] bytes) => endec.decodeFully(BinaryReaderDeserializer.of, new BinaryReader(new MemoryStream(bytes)));
}

internal class ByteDataHolder : ByteDataAccess {

    public byte[] bytes { get; init; }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        
        return this is ByteDataAccess access && bytes.Equals(access.bytes);
    }

    public override int GetHashCode() => bytes.GetHashCode();
}

internal sealed class EndecedStructWrapper<T> : StructWrapper, ByteDataAccess {
    private readonly T data;
    
    internal EndecedStructWrapper(T data, Endec<T> endec) : base(typeof(ByteDataAccess), WrappedType.Unknown) {
        this.data = data;
        bytes = ByteDataAccess.encodeAsBytes(endec, data);
    }
    
    public byte[] bytes { get; }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj is EndecedStructWrapper <T> other && data.Equals(other.data);
    }

    public override int GetHashCode() => data.GetHashCode();

    public override object Box() => data;

    public override void DisconnectFromPool() { /* NO OP */ }
    public override void Dispose() { /* NO OP */ }

    public override string ToString() => this.Unwrap<T>().ToString();

    public override string ToString(bool writeTypeInfo) => writeTypeInfo ? $"(EndecedStructWrapper<{wrappedType}>){this.Unwrap<T>().ToString()}" : ToString();
}