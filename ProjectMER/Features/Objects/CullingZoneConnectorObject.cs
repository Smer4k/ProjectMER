using LabApi.Features.Wrappers;
using ProjectMER.Features.Interfaces;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public sealed class CullingZoneConnectorObject : MonoBehaviour, ICullingContainer
{
    public static readonly List<CullingZoneConnectorObject> AllConnectors = [];
    public readonly List<CullingZoneObject> CullingZoneObjects = [];
    
    private void Start()
    {
        AllConnectors.Add(this);
    }

    private void OnDestroy()
    {
        AllConnectors.Remove(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        foreach (var zone in CullingZoneObjects)
        {
            zone.AddPlayer(player);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        foreach (var zone in CullingZoneObjects)
        {
            zone.RemovePlayer(player);
        }
    }

    public void AddPlayer(Player player)
    {
        foreach (var zone in CullingZoneObjects)
        {
            zone.AddPlayer(player);
        }
    }

    public void RemovePlayer(Player player)
    {
        foreach (var zone in CullingZoneObjects)
        {
            zone.RemovePlayer(player);
        }
    }
}