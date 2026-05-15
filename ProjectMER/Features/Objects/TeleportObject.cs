using LabApi.Features.Wrappers;

using ProjectMER.Features.Serializable;

using UnityEngine;

namespace ProjectMER.Features.Objects;

public class TeleportObject : MonoBehaviour
{
    private readonly Dictionary<Player, DateTime> cooldowns = [];

    private MapEditorObject _mapEditorObject;

    public SerializableTeleport Base;

    private void Start()
    {
        _mapEditorObject = GetComponent<MapEditorObject>();
        Base = (SerializableTeleport)_mapEditorObject.Base;
    }

    public TeleportObject? GetRandomTarget()
	{
        if (Base.Targets.Count == 0)
            return null;

        string targetId = Base.Targets.RandomItem();

		foreach (TeleportObject teleportObject in FindObjectsByType<TeleportObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
		{
			if (teleportObject._mapEditorObject.Id != targetId)
				continue;

			return teleportObject;
		}

		return null;
	}

	public void OnTriggerEnter(Collider other)
	{
        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
		if (player is null)
			return;

        if (cooldowns.TryGetValue(player, out DateTime next) && next > DateTime.Now)
            return;

        TeleportObject? target = GetRandomTarget();
		if (target == null)
			return;

        DateTime cooldownUntil = DateTime.Now.AddSeconds(Base.Cooldown);
        cooldowns[player] = cooldownUntil;
        target.cooldowns[player] = cooldownUntil;

        player.Position = target.gameObject.transform.position;
		player.LookRotation = target.gameObject.transform.eulerAngles;
	}
}
