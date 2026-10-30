using Mirror;
using UnityEngine;

namespace ProjectMER.Features.ClientSideToys;

public sealed class ClientSideToyAnchor : MonoBehaviour
{
    public uint NetId = NetworkIdentity.GetNextNetworkId();

    private void OnDestroy()
    {
        if (NetId != 0)
            ClientSideToyManager.Unregister(NetId);
    }
}
