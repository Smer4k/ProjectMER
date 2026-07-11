using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using PlayerRoles;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Interfaces;
using ProjectMER.Features.Objects;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace ProjectMER.Features.Serializable;

public class SerializablePlayerBlocker : SerializableObject, IIndicatorDefinition
{
    /// <summary>
    /// Gets or sets the <see cref="UnityEngine.PrimitiveType"/>.
    /// </summary>
    public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.Cube;

    public bool ItemsAllowed { get; set; } = true;
    public bool BulletsAllowed { get; set; } = true;
    public HashSet<RoleTypeId> Roles { get; set; } = [];

    private PlayerBlockerObject? _playerBlockerObject = null;

    public override GameObject SpawnOrUpdateObject(Room? room = null, GameObject? instance = null)
    {
        PrimitiveObjectToy primitive = instance == null
            ? UnityEngine.Object.Instantiate(PrefabManager.PrimitiveObject)
            : instance.GetComponent<PrimitiveObjectToy>();
        Vector3 position = room.GetAbsolutePosition(Position);
        Quaternion rotation = room.GetAbsoluteRotation(Rotation);
        _prevIndex = Index;

        primitive.transform.SetPositionAndRotation(position, rotation);
        primitive.transform.localScale = Scale;
        primitive.NetworkMovementSmoothing = 60;

        primitive.NetworkPrimitiveType = PrimitiveType;

        if (_playerBlockerObject == null)
        {
            _playerBlockerObject = primitive.gameObject.AddComponent<PlayerBlockerObject>();
        }
        
        _playerBlockerObject.BulletsAllowed = BulletsAllowed;
        _playerBlockerObject.ItemsAllowed = ItemsAllowed;
        _playerBlockerObject.Roles = Roles;
        _playerBlockerObject.UpdateVisibility();
        _playerBlockerObject.UpdateState();

        primitive.NetworkPrimitiveFlags = PrimitiveFlags.Collidable;

        if (instance == null)
            NetworkServer.Spawn(primitive.gameObject);

        return primitive.gameObject;
    }

    public GameObject SpawnOrUpdateIndicator(Room room, GameObject? instance = null)
    {
        PrimitiveObjectToy root;
        Vector3 position = room.GetAbsolutePosition(Position);
        Quaternion rotation = room.GetAbsoluteRotation(Rotation);

        if (instance == null)
        {
            root = UnityEngine.Object.Instantiate(PrefabManager.PrimitiveObject);
            root.NetworkPrimitiveFlags = PrimitiveFlags.Visible;
            root.NetworkMaterialColor = new Color(1, 0, 0, 0.5f);
        }
        else
        {
            root = instance.GetComponent<PrimitiveObjectToy>();
        }

        root.NetworkPrimitiveType = PrimitiveType;
        root.transform.position = position;
        root.transform.rotation = rotation;
        root.transform.localScale = Scale;

        return root.gameObject;
    }
}