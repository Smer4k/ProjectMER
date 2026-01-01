using PlayerRoles;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public class SchematicPlayerSpawnpointObject : MonoBehaviour
{
    public List<RoleTypeId> Roles { get; set; } = [];
    public static readonly List<SchematicPlayerSpawnpointObject> SpawnpointObjects = new();

    public void OnEnable()
    {
        SpawnpointObjects.Add(this);
    }

    public void OnDisable()
    {
        SpawnpointObjects.Remove(this);
    }
}