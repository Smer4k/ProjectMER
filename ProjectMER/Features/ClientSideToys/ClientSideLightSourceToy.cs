using AdminToys;
using Mirror;
using UnityEngine;

namespace ProjectMER.Features.ClientSideToys;

public sealed class ClientSideLightSourceToy : ClientSideAdminToy
{
    public float LightIntensity { get; set; }
    public float LightRange { get; set; }
    public Color LightColor { get; set; }
    public LightShadows ShadowType { get; set; }
    public float ShadowStrength { get; set; }
    public LightType LightType { get; set; }
#pragma warning disable CS0618 // Type or member is obsolete
    public LightShape LightShape { get; set; }
#pragma warning restore CS0618 // Type or member is obsolete
    public float SpotAngle { get; set; }
    public float InnerSpotAngle { get; set; }
    protected override uint AssetID => PrefabManager.LightSource.netIdentity.assetId;

    public ClientSideLightSourceToy()
    {
        
    }

    public ClientSideLightSourceToy(LightSourceToy toy) : base(toy)
    {
        LightIntensity = toy.LightIntensity;
        LightRange = toy.LightRange;
        LightColor = toy.LightColor;
        ShadowType = toy.ShadowType;
        ShadowStrength = toy.ShadowStrength;
        LightType = toy.LightType;
        LightShape = toy.LightShape;
        SpotAngle = toy.SpotAngle;
        InnerSpotAngle = toy.InnerSpotAngle;
    }
    
    protected override void WriteSyncVars(NetworkWriter writer)
    {
        base.WriteSyncVars(writer);
        writer.WriteFloat(LightIntensity);
        writer.WriteFloat(LightRange);
        writer.WriteColor(LightColor);
        writer.Write(ShadowType);
        writer.WriteFloat(ShadowStrength);
        writer.Write(LightType);
        writer.Write(LightShape);
        writer.WriteFloat(SpotAngle);
        writer.WriteFloat(InnerSpotAngle);
    }
}