using LabApi.Features.Wrappers;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public class SchematicTeleportObject : MonoBehaviour
{
	public DateTime NextTimeUse;
	public string Id { get; set; }
	public float Cooldown { get; set; } = 5f;
	public List<string> Targets { get; set; } = [];

	public SchematicTeleportObject? GetRandomTarget()
	{
		string targetId = Targets.RandomItem();

		foreach (SchematicTeleportObject teleportObject in FindObjectsByType<SchematicTeleportObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
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
		
		if (NextTimeUse > DateTime.Now)
			return;
		
		SchematicTeleportObject? target = GetRandomTarget();
		if (target == null)
			return;
		
		DateTime dateTime = DateTime.Now.AddSeconds(Cooldown);
		NextTimeUse = dateTime;
		target.NextTimeUse = dateTime;

		player.Position = target.gameObject.transform.position;
		player.LookRotation = target.gameObject.transform.eulerAngles;
	}
}