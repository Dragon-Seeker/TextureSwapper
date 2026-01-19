using System;
using io.wispforest.textureswapper.api.core;
using io.wispforest.textureswapper.utils;
using Photon.Pun;
using UnityEngine;

namespace io.wispforest.textureswapper.api.components;

public class MediaIdentifierComponent : MonoBehaviour, IPunObservable {
    private Identifier? id;

    public override bool Equals(object? obj) => obj is MediaIdentifierComponent otherId && getId().Equals(otherId.getId());

    public Identifier getId() => id ??= MediaIdentifiers.MISSING;
    
    public void setId(Identifier identifier) => id = identifier ?? MediaIdentifiers.ERROR;

    public override int GetHashCode() => getId().GetHashCode();

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info) {
        var prevId = id;

        try {
            stream.handleObject(() => {
                if (id == null) {
                    Plugin.Logger.LogWarning("Sent null id on MediaIdentifierComponent!");
                }
                
                return MediaSwapperStorage.getFullData(id ?? MediaIdentifiers.ERROR);
            }, value => {
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
            
            Plugin.Logger.LogError("Dumping the given PhotonStream to Log for debug info: ");
            stream.dumpStreamContents(Plugin.Logger.LogWarning);
        }
    }
}