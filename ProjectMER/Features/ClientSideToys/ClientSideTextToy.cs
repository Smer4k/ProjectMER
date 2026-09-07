using AdminToys;
using Mirror;
using UnityEngine;

namespace ProjectMER.Features.ClientSideToys;

public sealed class ClientSideTextToy : ClientSideAdminToy
{
    public Vector2 Size { get; set; }
    public string TextFormat { get; set; } = string.Empty;
    protected override uint AssetID => PrefabManager.Text.netIdentity.assetId;

    public ClientSideTextToy()
    {
        
    }

    public ClientSideTextToy(TextToy toy) : base(toy)
    {
        Size = toy.DisplaySize;
        TextFormat = toy.TextFormat;
    }
    
    protected override void WriteSyncVars(NetworkWriter writer)
    {
        base.WriteSyncVars(writer);
        writer.WriteVector2(Size);
        writer.WriteString(TextFormat);
    }

    protected override void WriteSyncObjects(NetworkWriter writer)
    {
        base.WriteSyncObjects(writer);
        writer.WriteUInt(0);
        writer.WriteUInt(0);
    }
}