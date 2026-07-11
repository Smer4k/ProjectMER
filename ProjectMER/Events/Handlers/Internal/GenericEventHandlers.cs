using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using ProjectMER.Features.ToolGun;
using UnityEngine;

namespace ProjectMER.Events.Handlers.Internal;

public class GenericEventsHandler : CustomEventsHandler
{
	public override void OnServerWaitingForPlayers()
	{
		PrefabManager.RegisterPrefabs();

		MapUtils.LoadedMaps.Clear();
		ToolGunItem.ItemDictionary.Clear();
		ToolGunHandler.PlayerSelectedObjectDict.Clear();
		PickupEventsHandler.ButtonPickups.Clear();
		PickupEventsHandler.PickupUsesLeft.Clear();
		FlickerController.Instances.Clear();
		FlickerController.FlickersBySchematic.Clear();
		FlickerController.FlickersByRoom.Clear();
	}

	public override void OnPlayerSpawning(PlayerSpawningEventArgs ev)
	{
		if (!ev.UseSpawnPoint)
			return;

		List<MonoBehaviour> list = [];
		foreach (MapSchematic map in MapUtils.LoadedMaps.Values)
		{
			foreach (KeyValuePair<string, SerializablePlayerSpawnpoint> spawnpoint in map.PlayerSpawnpoints)
			{
				if (!spawnpoint.Value.Roles.Contains(ev.Role.RoleTypeId))
					continue;

				list.AddRange(map.SpawnedObjects.Where(x => x.Id == spawnpoint.Key));
			}
		}

		foreach (var spawnpoint in SchematicPlayerSpawnpointObject.SpawnpointObjects)
		{
			if (!spawnpoint.Roles.Contains(ev.Role.RoleTypeId))
				continue;
			list.Add(spawnpoint);
		}

		if (list.Count == 0)
			return;

		MonoBehaviour randomElement = list[UnityEngine.Random.Range(0, list.Count)];

		ev.SpawnLocation = randomElement.transform.position;
		Timing.CallDelayed(0.05f, () =>
		{
			try
			{
				ev.Player.LookRotation = randomElement.transform.eulerAngles;
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		});
	}

	public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
	{
		foreach (var playerBlocker in PlayerBlockerObject.AllPlayerBlockers)
		{
			if (playerBlocker.Roles.Contains(ev.NewRole.RoleTypeId))
			{
				playerBlocker.HideForPlayer(ev.Player);
			}
			else
			{
				playerBlocker.ShowForPlayer(ev.Player);
			}
		}
	}

	public override void OnPlayerInteractingShootingTarget(PlayerInteractingShootingTargetEventArgs ev)
	{
		if (ev.ShootingTarget.GameObject.TryGetComponent(out MapEditorObject _))
			ev.IsAllowed = false;
	}
}
