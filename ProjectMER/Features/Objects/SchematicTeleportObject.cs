using LabApi.Features.Wrappers;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public class SchematicTeleportObject : MonoBehaviour
{
    private readonly Dictionary<Player, DateTime> _nextUsePerPlayer = new();
    public string Id { get; set; }
    public float Cooldown { get; set; } = 5f;
    public List<string> Targets { get; set; } = [];
    public SchematicObject? SchematicObject { get; private set; }
    
    private void OnDestroy()
    {
        if (SchematicObject == null)
            return;
        SchematicObject.TeleportObjects.Remove(Id);
    }

    public void Register(SchematicObject? schematicObject)
    {
        if (schematicObject == null || SchematicObject != null)
            return;
        SchematicObject = schematicObject;
        SchematicObject.TeleportObjects.Add(Id, this);
    }
    
    public SchematicTeleportObject? GetRandomTarget()
    {
        if (Targets.Count == 0 || SchematicObject == null)
            return null;
        
        return SchematicObject.TeleportObjects.GetValueOrDefault(Targets.RandomItem());
    }

    public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        
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