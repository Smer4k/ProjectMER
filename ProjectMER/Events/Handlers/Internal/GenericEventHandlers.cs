using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp079Events;
using LabApi.Events.CustomHandlers;
using LabApi.Features.Wrappers;
using MEC;
using NorthwoodLib.Pools;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using ProjectMER.Features.ToolGun;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace ProjectMER.Events.Handlers.Internal;

public class GenericEventsHandler : CustomEventsHandler
{
	public override void OnServerRoundRestarted()
	{
		PrefabManager.Reset();
	}

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

		if (ev.NewRole.RoleTypeId == RoleTypeId.Scp106)
		{
			foreach (var passableObject in Scp106PassableObject.AllPassableObjects)
			{
				passableObject.SetPassableFor(ev.Player, true);
			}
		} else if (ev.OldRole == RoleTypeId.Scp106)
		{
			foreach (var passableObject in Scp106PassableObject.AllPassableObjects)
			{
				passableObject.SetPassableFor(ev.Player, false);
			}
		}
	
		if (CullingZoneObject.AllCullingZone.Count == 0 || 
		    ev.Player.IsDestroyed || ev.Player.IsDummy || ev.Player.IsNpc)
		{
			return;
		}

		if (ev.OldRole == RoleTypeId.Scp079)
		{
			Timing.CallDelayed(0.5f, () =>
			{
				if (ev.Player == null || ev.Player.IsDestroyed || ev.NewRole.RoleTypeId == RoleTypeId.Scp079)
					return;
				foreach (var zone in CullingZoneObject.AllCullingZone)
				{
					zone.RemovePlayer(ev.Player);
				}
			});
		} else if (ev.NewRole.RoleTypeId == RoleTypeId.Filmmaker)
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
		} else if (ev.OldRole == RoleTypeId.Filmmaker)
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
		if (ev.Camera.Base.IsToy && ev.Camera.GameObject.TryGetComponent(out CameraTransferObject cameraTransferObject) 
		                         && ev.Player.RoleBase is Scp079Role scp079Role)
		{
			// Northwood epic moment
			// If you try to change the camera in `OnScp079ChangingCamera`, it will cause a bunch of extra event calls and drain more energy from SCP‑079 than necessary, so I have to use a workaround like this.
			var flag = cameraTransferObject.TargetCamera.Room.Zone == scp079Role._curCamSync.CurrentCamera.Room.Zone;
			var targetTime = flag ? 0.11f : 0.99f;
			ev.Camera = cameraTransferObject.TargetCamera;
			Timing.CallDelayed(targetTime, () =>
			{
				scp079Role._curCamSync.CurrentCamera = cameraTransferObject.TargetCamera.Base;
			});
		}

		if (CullingZoneObject.AllCullingZone.Count == 0)
			return;
		
		if (ev.Player.IsDestroyed || ev.Player.IsDummy || ev.Player.IsNpc)
			return;
		
		var targetCamera = ev.Camera.Base;
		var targets = ListPool<Player>.Shared.Rent();
		targets.AddRange(ev.Player.CurrentSpectators);
		targets.Add(ev.Player);
		
		foreach (var zone in CullingZoneObject.AllCullingZone)
		{
			foreach (var target in targets)
			{
				zone.RemovePlayer(target);
			}
		}
		
		var colliders = Physics.OverlapSphere(
			targetCamera.CameraAnchor.position,
			0.5f,
			-1,
			QueryTriggerInteraction.Collide);

		foreach (var collider in colliders)
		{
			if (collider.TryGetComponent(out CullingZoneObject cullingContainer))
			{
				foreach (var target in targets)
				{
					cullingContainer.AddPlayer(target);
					foreach (var connected in cullingContainer.ConnectedZones)
					{
						if (connected == null)
							continue;
						connected.AddPlayer(target);
					}
				}
			}
		}
		ListPool<Player>.Shared.Return(targets);
	}
}
