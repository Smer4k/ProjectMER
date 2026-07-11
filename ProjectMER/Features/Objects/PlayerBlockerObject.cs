using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace ProjectMER.Features.Objects;

public sealed class PlayerBlockerObject : MonoBehaviour
{
    public static readonly List<PlayerBlockerObject> AllPlayerBlockers = [];
    public bool BulletsAllowed = true;
    public bool ItemsAllowed = true;
    public HashSet<RoleTypeId> Roles = [];
    private readonly HashSet<Player> _ignoredPlayers = [];
    private PrimitiveObjectToy _primitive;
    private PrimitiveObjectToy? _hitbox;

    public void Start()
    {
        _primitive = GetComponent<PrimitiveObjectToy>();
        AllPlayerBlockers.Add(this);
    }

    public void OnDestroy()
    {
        AllPlayerBlockers.Remove(this);
    }

    public void HideForPlayer(Player player)
    {
        if (player.RoleBase is not IFpcRole fpcRole)
            return;
        if (_ignoredPlayers.Contains(player))
            return;
        if (_primitive == null || _primitive._collider == null)
            return;
        Physics.IgnoreCollision(fpcRole.FpcModule.CharController, _primitive._collider, true);
        player.ConnectionToClient.RemoveFromObserving(_primitive.netIdentity, false);
        _ignoredPlayers.Add(player);
    }

    public void ShowForPlayer(Player player)
    {
        if (player.RoleBase is not IFpcRole fpcRole)
            return;
        if (!_ignoredPlayers.Contains(player))
            return;
        if (_primitive == null || _primitive._collider == null)
            return;
        Physics.IgnoreCollision(fpcRole.FpcModule.CharController, _primitive._collider, false);
        player.ConnectionToClient.AddToObserving(_primitive.netIdentity);
        _ignoredPlayers.Remove(player);
    }

    public void UpdateVisibility()
    {
        foreach (var player in Player.ReadyList)
        {
            if (player == null || player.IsDestroyed)
                continue;
            if (Roles.Contains(player.Role))
                HideForPlayer(player);
            else
                ShowForPlayer(player);
        }
    }

    public void UpdateState()
    {
        if (_primitive == null)
            return;

        if (_hitbox != null)
        {
            NetworkServer.Destroy(_hitbox.gameObject);
            _hitbox = null;
        }

        if (ItemsAllowed && BulletsAllowed)
        {
            _primitive.gameObject.layer = LayerMask.NameToLayer("InvisibleCollider");
        }
        else if (ItemsAllowed)
        {
            _primitive.gameObject.layer = LayerMask.NameToLayer("InvisibleCollider");
            _hitbox = GameObject.Instantiate(PrefabManager.PrimitiveObject, _primitive.transform);
            _hitbox.NetworkPrimitiveType = _primitive.NetworkPrimitiveType;
            _hitbox.PrimitiveFlags = PrimitiveFlags.Collidable;
            _hitbox.gameObject.layer = LayerMask.NameToLayer("Hitbox");
            _hitbox.transform.SetPositionAndRotation(_primitive.transform.position, _primitive.transform.rotation);
            _hitbox.transform.localScale = _primitive.transform.localScale - new Vector3(0.01f, 0.01f, 0.01f);
            NetworkServer.Spawn(_hitbox.gameObject);
        }
        else if (BulletsAllowed)
        {
            _primitive.gameObject.layer = LayerMask.NameToLayer("Fence");
        }
        else
        {
            _primitive.gameObject.layer = LayerMask.NameToLayer("Default");
        }
    }
}