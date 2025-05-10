using System;
using io.wispforest.textureswapper.utils;
using Photon.Pun;
using UnityEngine;

namespace io.wispforest.textureswapper.api.components;

public class MediaIdentifierComponent : MonoBehaviour, IPunObservable {
    private Identifier? id;

    public override bool Equals(object? obj) {
        if (obj is MediaIdentifierComponent otherId) {
            return getId().Equals(otherId.getId());
        }
        
        return false;
    }

    public Identifier getId() {
        return id ??= MediaIdentifiers.MISSING;
    }
    
    public void setId(Identifier identifier) {
        id = identifier ?? MediaIdentifiers.ERROR;
    }

    public override int GetHashCode() {
        return getId().GetHashCode();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
        var prevId = id;

        try {
            stream.handleObject(() => MediaSwapperStorage.getFullData(id ?? MediaIdentifiers.ERROR)!, value => {
                if (value is null) {
                    Plugin.logIfDebugging(source => source.LogError($"Unable to setup material on client as the object decoded as null!"));

                    return;
                }

                if (value.id.Equals(prevId)) return;

                SwapperComponentSetupUtils.clientPaintingDataChange(gameObject, value);

                this.setId(value.id);
            });
        } catch (Exception e) {
            Plugin.Logger.LogError("Unable to handle networking for media id component: ");
            Plugin.Logger.LogError(e);
            
            stream.dumpPhotonStreamToLog(logMessageAction => Plugin.logIfDebugging(source => logMessageAction(source.LogWarning)));
        }
    }
}