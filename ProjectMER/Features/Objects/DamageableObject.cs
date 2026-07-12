using System.Collections;
using AdminToys;
using PlayerRoles;
using PlayerStatsSystem;
using ProjectMER.Features.Serializable.Schematics;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public sealed class DamageableObject : MonoBehaviour, IDestructible
{
    // for plugins
    public static event Action<DamageableObject> OnDamage;
    public static event Action<DamageableObject> OnDeath;
    public static event Action<DamageableObject> OnRegisterComplete;

    public uint NetworkId => _primitive.netId;
    public Vector3 CenterOfMass => transform.position;
    public int ObjectId;
    public SchematicObject? SchematicObject;

    public float Health = 100;
    public readonly HashSet<RoleTypeId> Roles = [];
    public bool RegistrationComplete { get; private set; }

    public readonly HashSet<ExplosionType> ExplosionTypes =
    [
        ExplosionType.Grenade,
        ExplosionType.SCP018,
        ExplosionType.PinkCandy,
        ExplosionType.Cola,
        ExplosionType.Disruptor,
        ExplosionType.Jailbird,
        ExplosionType.Custom
    ];

    public readonly HashSet<ItemType> Weapons = [];

    private static readonly WaitForEndOfFrame WaitForEndOfFrame = new WaitForEndOfFrame();
    private PrimitiveObjectToy _primitive;

    private void Awake()
    {
        _primitive = GetComponent<PrimitiveObjectToy>();
    }

    public bool Damage(float damage, DamageHandlerBase handler, Vector3 exactHitPos)
    {
        if (!RegistrationComplete)
            return false;
        if (Health <= 0)
            return false;
        if (handler is not AttackerDamageHandler attackerDamageHandler)
            return false;
        var role = attackerDamageHandler.Attacker.Role;
        if (!CheckDamagePerms(role))
            return false;
        switch (handler)
        {
            case ExplosionDamageHandler explosionDamageHandler
                when !CheckDamagePerms(explosionDamageHandler.ExplosionType):
            case FirearmDamageHandler firearmDamageHandler when !CheckDamagePerms(firearmDamageHandler.WeaponType):
                return false;
            default:
                ServerDamageObject(damage);
                return true;
        }
    }

    public void ServerDamageObject(float damage)
    {
        if (Health <= 0)
            return;
        Health -= damage;
        if (Health > 0)
        {
            OnDamage?.Invoke(this);
            if (SchematicObject != null)
                SchematicObject.RunActionsByEventId(ObjectId, nameof(OnDamage));
            return;
        }

        OnDeath?.Invoke(this);
        if (SchematicObject != null)
            SchematicObject.RunActionsByEventId(ObjectId, nameof(OnDeath));
    }

    public bool CheckDamagePerms(RoleTypeId roleType)
    {
        return Roles.Count == 0 || Roles.Contains(roleType);
    }

    public bool CheckDamagePerms(ItemType weapon)
    {
        return Weapons.Count == 0 || Weapons.Contains(weapon);
    }

    public bool CheckDamagePerms(ExplosionType explosionType)
    {
        return ExplosionTypes.Contains(explosionType);
    }

    public void RegisterChildDestructibles(List<SchematicBlockData>? blocks = null)
    {
        List<Transform>? targets = null;
        if (blocks != null && SchematicObject != null)
        {
            targets = new List<Transform>();
            var ids = new HashSet<int> { ObjectId };
            foreach (var block in blocks)
            {
                if (!ids.Contains(block.ParentId)
                    || !SchematicObject.ObjectFromId.TryGetValue(block.ObjectId, out var target)
                    || target == null) continue;
                ids.Add(block.ObjectId);
                targets.Add(target);
            }
        }

        StartCoroutine(CoroutineRegisterChildDestructibles(targets));
    }

    private IEnumerator CoroutineRegisterChildDestructibles(List<Transform>? targets)
    {
        if (targets == null)
        {
            foreach (Transform child in transform)
            {
                yield return RegisterChildDestructiblesRecursive(child);
            }
        }
        else
        {
            foreach (var target in targets)
            {
                yield return RegisterChildDestructiblesRecursive(target);
            }
        }

        RegistrationComplete = true;
        OnRegisterComplete?.Invoke(this);
    }

    private IEnumerator RegisterChildDestructiblesRecursive(Transform current)
    {
        if (current.TryGetComponent<DamageableObject>(out _))
            yield break;

        if (current.TryGetComponent<PlayerBlockerObject>(out var playerBlocker) && playerBlocker.Hitbox != null)
        {
            foreach (Transform grandChild in current)
            {
                if (playerBlocker.Hitbox.transform == grandChild)
                    continue;
                yield return RegisterChildDestructiblesRecursive(grandChild);
            }

            yield break;
        }

        if (current.TryGetComponent(out Collider collider)
            && !collider.isTrigger
            && !current.TryGetComponent<SchematicTeleportObject>(out _)
            && !current.TryGetComponent<SchematicPlayerSpawnpointObject>(out _)
            && !current.TryGetComponent<PlayerBlockerObject>(out _))
        {
            if (!current.TryGetComponent(out DamageableObjectChild childComponent))
                childComponent = current.gameObject.AddComponent<DamageableObjectChild>();
            childComponent.Initialize(this);
        }

        yield return WaitForEndOfFrame;

        foreach (Transform grandChild in current)
        {
            yield return RegisterChildDestructiblesRecursive(grandChild);
        }
    }
}