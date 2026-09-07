using AdminToys;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace ProjectMER.Features.Objects;

public sealed class Scp106PassableObject : MonoBehaviour
{
    public static readonly List<Scp106PassableObject> AllPassableObjects = [];
    public const string ColliderName = "Scp106PassableObject4jnrthfddfgmrt";
    private PrimitiveObjectToy _visual;
    public PrimitiveObjectToy Collider { get; private set; }
    
    private void Start()
    {
        _visual = GetComponent<PrimitiveObjectToy>();
        Collider = LabApi.Features.Wrappers.PrimitiveObjectToy.Create(transform).Base;
        Collider.gameObject.name = ColliderName;
        Collider.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        Refresh();
        AllPassableObjects.Add(this);
    }

    public void OnDestroy()
    {
        AllPassableObjects.Remove(this);
        if (Collider == null)
            return;
        NetworkServer.Destroy(Collider.gameObject);
    }

    public void Refresh()
    {
        if (Collider == null)
            return;
        if (_visual.NetworkPrimitiveFlags.HasFlag(PrimitiveFlags.Collidable))
            _visual.NetworkPrimitiveFlags &= ~PrimitiveFlags.Collidable;
        
        Collider.NetworkIsStatic = true;
        Collider.NetworkPrimitiveFlags = PrimitiveFlags.Collidable;
        if (_visual.NetworkPrimitiveType != Collider.NetworkPrimitiveType)
        {
            Collider.NetworkPrimitiveType = _visual.NetworkPrimitiveType;
        }

        Timing.CallDelayed(1f, () =>
        {
            foreach (var player in Player.ReadyList)
            {
                if (player.Role != RoleTypeId.Scp106 || player.RoleBase is not IFpcRole fpcRole) 
                    continue;
                player.ConnectionToClient.RemoveFromObserving(Collider.netIdentity, false);
                Physics.IgnoreCollision(fpcRole.FpcModule.CharController, Collider._collider, true);
            }
        });
    }

    public void SetPassableFor(Player player, bool canPassable)
    {
        if (player.RoleBase is not IFpcRole fpcRole)
            return;
        if (canPassable)
        {
            player.ConnectionToClient.RemoveFromObserving(Collider.netIdentity, false);
            Collider.netIdentity.RemoveObserver(player.ConnectionToClient);
            Physics.IgnoreCollision(fpcRole.FpcModule.CharController, Collider._collider, true);
        }
        else
        {
            Collider.netIdentity.AddObserver(player.ConnectionToClient);
            Physics.IgnoreCollision(fpcRole.FpcModule.CharController, Collider._collider, false);
        }
    }

    public void RefreshPassableFor(Player player)
    {
        if (player.RoleBase is not IFpcRole)
            return;
        SetPassableFor(player, player.Role == RoleTypeId.Scp106);
    }
}