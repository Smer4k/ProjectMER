using AdminToys;
using Mirror;
using UnityEngine;

namespace ProjectMER.Features.ClientSideToys;

public sealed class ClientSideCullingParent : ClientSideAdminToy
{
    public Vector3 Size { get; set; }
    public Vector3 BoundsPosition { get; set; }
    
    protected override uint AssetID => PrefabManager.CullingParent.netIdentity.assetId;
    
    public ClientSideCullingParent()
    {
        
    }

    public ClientSideCullingParent(SpawnableCullingParent toy)
    {
        var transform = toy.transform;
        LocalPosition = transform.position;
        LocalRotation = transform.rotation;
        Scale = transform.localScale;
        NetId = toy.netIdentity.netId;
    }
    
    protected override void WriteSyncVars(NetworkWriter writer)
    {
        writer.WriteVector3(BoundsPosition);
        writer.WriteVector3(Size);
    }
}