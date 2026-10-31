using AdminToys;
using Mirror;

namespace ProjectMER.Features.ClientSideToys;

public sealed class ClientSideCapybara : ClientSideAdminToy
{
    public bool Collidable { get; set; }
    protected override uint AssetID => PrefabManager.Capybara.netIdentity.assetId;

    public ClientSideCapybara()
    {

    }

    public ClientSideCapybara(CapybaraToy capybaraToy) : base(capybaraToy)
    {
        Collidable = capybaraToy.NetworkCollisionsEnabled;
    }
    
    protected override void WriteSyncVars(NetworkWriter writer)
    {
        base.WriteSyncVars(writer);
        writer.WriteBool(Collidable);
    }
}