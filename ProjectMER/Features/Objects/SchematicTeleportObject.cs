using LabApi.Features.Wrappers;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public class SchematicTeleportObject : MonoBehaviour
{
    private readonly Dictionary<Player, DateTime> _nextUsePerPlayer = new();
    public string Id { get; set; }
    public float Cooldown { get; set; } = 5f;
    public List<string> Targets { get; set; } = [];

    public SchematicTeleportObject? GetRandomTarget()
    {
        string targetId = Targets.RandomItem();

        foreach (SchematicTeleportObject teleportObject in FindObjectsByType<SchematicTeleportObject>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (teleportObject.Id != targetId)
            {
                continue;
            }

            return teleportObject;
        }

        return null;
    }

    public void OnTriggerEnter(Collider other)
    {
        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        if (_nextUsePerPlayer.TryGetValue(player, out DateTime nextUse) && nextUse > DateTime.Now)
            return;

        SchematicTeleportObject? target = GetRandomTarget();
        if (target == null)
            return;

        DateTime newCooldown = DateTime.Now.AddSeconds(Cooldown);
        _nextUsePerPlayer[player] = newCooldown;
        target._nextUsePerPlayer[player] = newCooldown;

        player.Position = target.gameObject.transform.position;
        player.LookRotation = target.gameObject.transform.eulerAngles;
    }
}