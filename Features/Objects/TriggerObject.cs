using LabApi.Features.Wrappers;
using ProjectMER.Features.Enums;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public class TriggerObject : MonoBehaviour
{
    public int ObjectId;
    public SchematicObject SchematicObject;
    public Action<Player> OnPlayerEnter;
    public Action<Player> OnPlayerExit;
    public TriggerTargetType TargetType = TriggerTargetType.Player;

    public void OnTriggerEnter(Collider other)
    {
        if (TargetType == TriggerTargetType.None)
            return;
        
        Player? player = null;
        if (TargetType == TriggerTargetType.Player)
        {
            if (!other.CompareTag("Player"))
                return;

            player = Player.Get(other.gameObject);
            if (player != null)
                OnPlayerEnter?.Invoke(player);
        } else if (TargetType == TriggerTargetType.Trigger)
        {
            if (!other.TryGetComponent(out TriggerObject triggerObject))
                return;
            player = Player.Get(triggerObject.GetComponentInParent<ReferenceHub>());
        }

        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnTriggerEnter), player);
    }

    public void OnTriggerExit(Collider other)
    {
        if (TargetType == TriggerTargetType.None)
            return;
        
        Player? player = null;
        if (TargetType == TriggerTargetType.Player)
        {
            if (!other.CompareTag("Player"))
                return;

            player = Player.Get(other.gameObject);
            if (player != null)
                OnPlayerExit?.Invoke(player);
        } else if (TargetType == TriggerTargetType.Trigger)
        {
            if (!other.TryGetComponent(out TriggerObject triggerObject))
                return;
            player = Player.Get(triggerObject.GetComponentInParent<ReferenceHub>());
        }

        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnTriggerExit), player);
    }

    public void OnTriggerStay(Collider other)
    {
        if (TargetType == TriggerTargetType.None)
            return;
        
        Player? player = null;
        if (TargetType == TriggerTargetType.Player)
        {
            if (!other.CompareTag("Player"))
                return;

            player = Player.Get(other.gameObject);
        } else if (TargetType == TriggerTargetType.Trigger)
        {
            if (!other.TryGetComponent(out TriggerObject triggerObject))
                return;
            player = Player.Get(triggerObject.GetComponentInParent<ReferenceHub>());
        }

        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnTriggerStay), player);
    }
}