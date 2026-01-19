using System;
using System.Security.Cryptography;
using System.Text;
using io.wispforest.endec;
using io.wispforest.textureswapper.api.core;

namespace io.wispforest.textureswapper.utils;

public static class GuidUtils {

    public static readonly Endec<Guid> BYTE_ENDEC = 
        Endec.BYTES.xmap(bytes => new Guid(bytes), guid => guid.ToByteArray());
    
    public static readonly Endec<Guid> STRING_ENDEC = 
        Endec.STRING.xmap(bytes => new Guid(bytes), guid => guid.ToString());

    public static readonly Endec<Guid> ENDEC = Endec.ifAttr(SerializationAttributes.HUMAN_READABLE, STRING_ENDEC).orElse(BYTE_ENDEC);
    
    public static Guid shaToGuid(this Identifier input) {
        return input.ToString().shaToGuid();
    }

    public static Guid shaToGuid(this string input) {
        using var sha1 = SHA1.Create();
        
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            
        // GUIDs are 16 bytes (128 bits). SHA-1 is 20 bytes, so we truncate.
        var newGuid = new byte[16];
        Array.Copy(hash, 0, newGuid, 0, 16);

        // Set the version to 5 (Name-based SHA-1)
        // The version is in the 7th byte, high 4 bits (0x50)
        newGuid[6] = (byte)((newGuid[6] & 0x0F) | 0x50);
            
        // Set the variant to RFC 4122 (0x80)
        // The variant is in the 9th byte, high 2 bits
        newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

        return new Guid(newGuid);
    }
}