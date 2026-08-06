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
    public const string ColliderName = "Scp106PassableObject";
    private PrimitiveObjectToy _visual;
    private PrimitiveObjectToy _collider;
    
    private void Start()
    {
        _visual = GetComponent<PrimitiveObjectToy>();
        _collider = LabApi.Features.Wrappers.PrimitiveObjectToy.Create(transform).Base;
        _collider.gameObject.name = ColliderName;
        _collider.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        Refresh();
        AllPassableObjects.Add(this);
    }

    public void OnDestroy()
    {
        AllPassableObjects.Remove(this);
        if (_collider == null)
            return;
        NetworkServer.Destroy(_collider.gameObject);
    }

    public void Refresh()
    {
        if (_collider == null)
            return;
        if (_visual.NetworkPrimitiveFlags.HasFlag(PrimitiveFlags.Collidable))
            _visual.NetworkPrimitiveFlags &= ~PrimitiveFlags.Collidable;
        
        _collider.NetworkIsStatic = true;
        _collider.NetworkPrimitiveFlags = PrimitiveFlags.Collidable;
        if (_visual.NetworkPrimitiveType != _collider.NetworkPrimitiveType)
        {
            _collider.NetworkPrimitiveType = _visual.NetworkPrimitiveType;
        }

        Timing.CallDelayed(1f, () =>
        {
            foreach (var player in Player.ReadyList)
            {
                if (player.Role != RoleTypeId.Scp106 || player.RoleBase is not IFpcRole fpcRole) 
                    continue;
                player.ConnectionToClient.RemoveFromObserving(_collider.netIdentity, false);
                Physics.IgnoreCollision(fpcRole.FpcModule.CharController, _collider._collider, true);
            }
        });
    }

    public void SetPassableFor(Player player, bool canPassable)
    {
        if (player.RoleBase is not IFpcRole fpcRole)
            return;
        if (canPassable)
        {
            player.ConnectionToClient.RemoveFromObserving(_collider.netIdentity, false);
            Physics.IgnoreCollision(fpcRole.FpcModule.CharController, _collider._collider, true);
        }
        else
        {
            player.ConnectionToClient.AddToObserving(_collider.netIdentity);
            Physics.IgnoreCollision(fpcRole.FpcModule.CharController, _collider._collider, false);
        }
    }
}