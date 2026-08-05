using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp079Events;
using LabApi.Events.CustomHandlers;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Interfaces;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using ProjectMER.Features.ToolGun;
using UnityEngine;
using UserSettings.ServerSpecific;

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

	public override void OnPlayerJoined(PlayerJoinedEventArgs ev)
	{
		if (ServerSpecificSettingsSync.DefinedSettings == null)
			return;
		var settings = ServerSpecificSettingsSync.DefinedSettings.Where(x =>
			x is not SSDropdownSetting { SettingId: ProjectMER.MerSettingId }
				and not SSGroupHeader { Label: "ProjectMER" }).ToArray();
		ev.Player.ConnectionToClient.Send<SSSEntriesPack>(new SSSEntriesPack(settings,
			ServerSpecificSettingsSync.Version));
	}

	public override void OnPlayerSpawning(PlayerSpawningEventArgs ev)
	{
		if (!ev.Role.ServerSpawnFlags.HasFlag(RoleSpawnFlags.UseSpawnpoint))
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
	
		if (ev.Player.IsDummy || ev.Player.IsNpc)
		{
			return;
		}

		if (ev.OldRole == RoleTypeId.Scp079)
		{
			Timing.CallDelayed(0.5f, () =>
			{
				if (ev.Player == null || ev.Player.IsDestroyed || ev.NewRole.RoleTypeId == RoleTypeId.Scp079)
					return;
				foreach (var connector in CullingZoneConnectorObject.AllConnectors)
				{
					connector.RemovePlayer(ev.Player);
				}
		
				foreach (var zone in CullingZoneObject.AllCullingZone)
				{
					zone.RemovePlayer(ev.Player);
				}
			});
		}

		if (ev.NewRole.RoleTypeId == RoleTypeId.Filmmaker)
		{
			Timing.CallDelayed(0.5f, () =>
			{
				if (ev.Player == null || ev.Player.IsDestroyed || ev.NewRole.RoleTypeId != RoleTypeId.Filmmaker)
					return;
				foreach (var zone in CullingZoneObject.AllCullingZone)
				{
					zone.AddPlayer(ev.Player);
				}
			});
		}

		if (ev.OldRole == RoleTypeId.Filmmaker)
		{
			Timing.CallDelayed(0.5f, () =>
			{
				if (ev.Player == null || ev.Player.IsDestroyed || ev.NewRole.RoleTypeId == RoleTypeId.Filmmaker)
					return;
				foreach (var zone in CullingZoneObject.AllCullingZone)
				{
					zone.RemovePlayer(ev.Player);
				}
			});
		}
	}

	public override void OnPlayerInteractingShootingTarget(PlayerInteractingShootingTargetEventArgs ev)
	{
		if (ev.ShootingTarget.GameObject.TryGetComponent(out MapEditorObject _))
			ev.IsAllowed = false;
	}

	public override void OnPlayerChangedSpectator(PlayerChangedSpectatorEventArgs ev)
	{
		if (CullingZoneObject.AllCullingZone.Count == 0)
			return;
		if (ev.Player == null || ev.Player.IsDestroyed || ev.Player.IsNpc || ev.Player.IsDummy || ev.NewTarget == null)
			return;

		foreach (var zone in CullingZoneObject.AllCullingZone)
		{
			if (ev.OldTarget != null && zone.Contains(ev.OldTarget) && !zone.Contains(ev.NewTarget))
			{
				zone.HideFor(ev.Player);
			}

			if (zone.Contains(ev.NewTarget) && (ev.OldTarget == null || !zone.Contains(ev.OldTarget)))
			{
				zone.ShowFor(ev.Player);
			}
		}
	}

	public override void OnScp079ChangedCamera(Scp079ChangedCameraEventArgs ev)
	{
		if (ev.Player.IsDestroyed || ev.Player.IsDummy || ev.Player.IsNpc)
			return;
		
		if (CullingZoneObject.AllCullingZone.Count == 0 && CullingZoneConnectorObject.AllConnectors.Count == 0)
			return;
		
		foreach (var connector in CullingZoneConnectorObject.AllConnectors)
		{
			connector.RemovePlayer(ev.Player);
		}
		
		foreach (var zone in CullingZoneObject.AllCullingZone)
		{
			zone.RemovePlayer(ev.Player);
		}
		
		var colliders = Physics.OverlapSphere(
			ev.Camera.Base.CameraAnchor.position,
			0.5f,
			-1,
			QueryTriggerInteraction.Collide);

		foreach (var collider in colliders)
		{
			if (collider.TryGetComponent(out ICullingContainer cullingContainer))
			{
				cullingContainer.AddPlayer(ev.Player);
			}
		}
	}
}
