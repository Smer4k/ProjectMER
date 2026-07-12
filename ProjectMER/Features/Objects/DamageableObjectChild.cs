using PlayerStatsSystem;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public sealed class DamageableObjectChild : MonoBehaviour, IDestructible
{
    private DamageableObject _parent;

    public uint NetworkId => _parent.NetworkId;
    public Vector3 CenterOfMass => transform.position;

    public void Initialize(DamageableObject parent)
    {
        _parent = parent;
    }

    public bool Damage(float damage, DamageHandlerBase handler, Vector3 exactHitPos)
    {
        return _parent.Damage(damage, handler, exactHitPos);
    }
}