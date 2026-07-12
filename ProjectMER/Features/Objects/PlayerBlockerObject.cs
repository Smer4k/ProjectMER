using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using UnityEngine;
using UnityEngine.Serialization;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace ProjectMER.Features.Objects;

public sealed class PlayerBlockerObject : MonoBehaviour
{
    public static readonly List<PlayerBlockerObject> AllPlayerBlockers = [];
    public bool BulletsAllowed = true;
    public bool ItemsAllowed = true;
    public HashSet<RoleTypeId> Roles = [];
    public PrimitiveObjectToy? Hitbox { get; private set; }
    private readonly HashSet<Player> _ignoredPlayers = [];
    private PrimitiveObjectToy _primitive;

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

        if (Hitbox != null)
        {
            NetworkServer.Destroy(Hitbox.gameObject);
            Hitbox = null;
        }

        if (ItemsAllowed && BulletsAllowed)
        {
            _primitive.gameObject.layer = LayerMask.NameToLayer("InvisibleCollider");
        }
        else if (ItemsAllowed)
        {
            _primitive.gameObject.layer = LayerMask.NameToLayer("InvisibleCollider");
            Hitbox = GameObject.Instantiate(PrefabManager.PrimitiveObject, _primitive.transform);
            Hitbox.NetworkPrimitiveType = _primitive.NetworkPrimitiveType;
            Hitbox.PrimitiveFlags = PrimitiveFlags.Collidable;
            Hitbox.gameObject.layer = LayerMask.NameToLayer("Hitbox");
            Hitbox.transform.SetPositionAndRotation(_primitive.transform.position, _primitive.transform.rotation);
            Hitbox.transform.localScale = _primitive.transform.localScale - new Vector3(0.01f, 0.01f, 0.01f);
            NetworkServer.Spawn(Hitbox.gameObject);
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