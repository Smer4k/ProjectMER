using AdminToys;
using Mirror;
using UnityEngine;

namespace ProjectMER.Features.ClientSideToys;

public sealed class ClientSidePrimitive : ClientSideAdminToy
{
    public PrimitiveType Type { get; set; }
    public Color Color { get; set; }
    public PrimitiveFlags Flags { get; set; }
    protected override uint AssetID => PrefabManager.PrimitiveObject.netIdentity.assetId;

    public ClientSidePrimitive()
    {
        
    }

    public ClientSidePrimitive(PrimitiveObjectToy toy) : base(toy)
    {
        Color = toy.MaterialColor;
        Flags = toy.PrimitiveFlags;
        Type = toy.PrimitiveType;
    }
    
    protected override void WriteSyncVars(NetworkWriter writer)
    {
        base.WriteSyncVars(writer);
        writer.Write(Type);
        writer.WriteColor(Color);
        writer.Write(Flags);
    }
}