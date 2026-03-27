using LabApi.Features.Wrappers;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public class TriggerObject : MonoBehaviour
{
    public Action<Player> OnPlayerEnter;
    public Action<Player> OnPlayerExit;
    
    public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        
        Player? player = Player.Get(other.gameObject);
        if (player == null)
            return;
        
        OnPlayerEnter?.Invoke(player);
    }

    public void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        
        Player? player = Player.Get(other.gameObject);
        if (player == null)
            return;
        
        OnPlayerExit?.Invoke(player);
    }
}