using LabApi.Features.Wrappers;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public class TriggerObject : MonoBehaviour
{
    public int ObjectId;
    public SchematicObject SchematicObject;
    public Action<Player> OnPlayerEnter;
    public Action<Player> OnPlayerExit;

    public void Initialize(SchematicObject schematicObject, int objectId)
    {
        ObjectId = objectId;
        SchematicObject = schematicObject;
    }
    
    public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        
        Player? player = Player.Get(other.gameObject);
        if (player == null)
            return;
        
        OnPlayerEnter?.Invoke(player);
        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnTriggerEnter), player);
    }

    public void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        
        Player? player = Player.Get(other.gameObject);
        if (player == null)
            return;
        
        OnPlayerExit?.Invoke(player);
        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnTriggerExit), player);
    }

    public void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        
        Player? player = Player.Get(other.gameObject);
        if (player == null)
            return;
        
        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnTriggerStay), player);
    }
}