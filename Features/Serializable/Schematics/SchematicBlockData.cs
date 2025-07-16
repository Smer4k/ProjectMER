using AdminToys;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Pickups;
using LabApi.Features.Wrappers;
using MapGeneration.Distributors;
using MEC;
using Mirror;
using ProjectMER.Events.Handlers.Internal;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Lockers;
using UnityEngine;
using Utf8Json;
using CameraType = ProjectMER.Features.Enums.CameraType;
using CapybaraToy = AdminToys.CapybaraToy;
using LightSourceToy = AdminToys.LightSourceToy;
using LabApiLocker = LabApi.Features.Wrappers.Locker;
using LapApiLockerChamber = LabApi.Features.Wrappers.LockerChamber;
using Locker = MapGeneration.Distributors.Locker;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using TextToy = AdminToys.TextToy;

namespace ProjectMER.Features.Serializable.Schematics;

public class SchematicBlockData
{
	public virtual string Name { get; set; }

	public virtual int ObjectId { get; set; }

	public virtual int ParentId { get; set; }

	public virtual string AnimatorName { get; set; }

	public virtual Vector3 Position { get; set; }

	public virtual Vector3 Rotation { get; set; }

	public virtual Vector3 Scale { get; set; }

	public virtual BlockType BlockType { get; set; }

	public virtual Dictionary<string, object> Properties { get; set; }

	public GameObject Create(SchematicObject schematicObject, Transform parentTransform)
	{
		var commonBlock = BlockType is BlockType.Light or BlockType.Empty or BlockType.Interactable
			or BlockType.Primitive or BlockType.Schematic or BlockType.Pickup;

		if (!commonBlock)
		{
			var mapObjs =
				GameObject.FindObjectsByType<MapEditorObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (var mapObj in mapObjs)
			{
				if (mapObj == null || mapObj.MapName == null) continue;
				if (!MapUtils.LoadedMaps.TryGetValue(mapObj.MapName, out _) && mapObj.Id == Name)
				{
					GameObject obj = CreateEmpty();
					obj.name = Name;
					Transform trans = obj.transform;
					trans.SetParent(parentTransform);
					trans.SetLocalPositionAndRotation(Position, Quaternion.Euler(Rotation));
					trans.localScale = BlockType == BlockType.Empty && Scale == Vector3.zero ? Vector3.one : Scale;
					return obj;
				}
			}
		}

		GameObject gameObject = BlockType switch
		{
			BlockType.Empty => CreateEmpty(),
			BlockType.Primitive => CreatePrimitive(),
			BlockType.Light => CreateLight(),
			BlockType.Pickup => CreatePickup(schematicObject),
			BlockType.Workstation => CreateWorkstation(),
			BlockType.Teleport => CreateTeleport(parentTransform),
			BlockType.Locker => CreateLocker(),
			BlockType.Door => CreateDoor(),
			BlockType.Interactable => CreateInteractable(),
			BlockType.Text => CreateText(),
			BlockType.Camera => CreateCamera(),
			BlockType.ShootingTarget => CreateShootingTarget(),
			BlockType.PlayerSpawnPoint => CreatePlayerSpawnPoint(),
			BlockType.Capybara => CreateCapybara(),
			_ => CreateEmpty(true)
		};

		gameObject.name = Name;

		Transform transform = gameObject.transform;
		transform.SetParent(parentTransform);
		transform.SetLocalPositionAndRotation(Position, Quaternion.Euler(Rotation));
		transform.localScale = BlockType == BlockType.Empty && Scale == Vector3.zero ? Vector3.one : Scale;

		// if you don't remove the parent before NetworkServer.Spawn then there won't be a door
		if (BlockType == BlockType.Door)
		{
			transform.SetParent(null);
		}

		if (gameObject.TryGetComponent(out StructurePositionSync structurePositionSync))
		{
			structurePositionSync.Network_position = gameObject.transform.position;
			structurePositionSync.Network_rotationY =
				(sbyte)Mathf.RoundToInt(gameObject.transform.rotation.eulerAngles.y / 5.625f);
		}

		if (BlockType == BlockType.Teleport)
			transform.position += Vector3.up;

		return gameObject;
	}

	private GameObject CreateEmpty(bool fallback = false)
	{
		if (fallback)
			Logger.Warn($"{BlockType} is not yet implemented. Object will be an empty GameObject instead.");

		PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);
		primitive.NetworkPrimitiveFlags = PrimitiveFlags.None;
		primitive.NetworkMovementSmoothing = 60;

		return primitive.gameObject;
	}

	private GameObject CreatePrimitive()
	{
		PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);
		primitive.NetworkMovementSmoothing = 60;

		primitive.NetworkPrimitiveType = (PrimitiveType)Convert.ToInt32(Properties["PrimitiveType"]);
		primitive.NetworkMaterialColor = Properties["Color"].ToString().GetColorFromString();

		PrimitiveFlags primitiveFlags;
		if (Properties.TryGetValue("PrimitiveFlags", out object flags))
		{
			primitiveFlags = (PrimitiveFlags)Convert.ToByte(flags);
		}
		else
		{
			// Backward compatibility
			primitiveFlags = PrimitiveFlags.Visible;
			if (Scale.x >= 0f)
				primitiveFlags |= PrimitiveFlags.Collidable;
		}

		primitive.NetworkPrimitiveFlags = primitiveFlags;

		return primitive.gameObject;
	}

	private GameObject CreateLight()
	{
		LightSourceToy light = GameObject.Instantiate(PrefabManager.LightSource);
		light.NetworkMovementSmoothing = 60;

		light.NetworkLightType = Properties.TryGetValue("LightType", out object lightType) ? (LightType)Convert.ToInt32(lightType) : LightType.Point;
		light.NetworkLightColor = Properties["Color"].ToString().GetColorFromString();
		light.NetworkLightIntensity = Convert.ToSingle(Properties["Intensity"]);
		light.NetworkLightRange = Convert.ToSingle(Properties["Range"]);

		if (Properties.TryGetValue("Shadows", out object shadows))
		{
			// Backward compatibility
			light.NetworkShadowType = Convert.ToBoolean(shadows) ? LightShadows.Soft : LightShadows.None;
		}
		else
		{
			light.NetworkShadowType = (LightShadows)Convert.ToInt32(Properties["ShadowType"]);
			light.NetworkLightShape = (LightShape)Convert.ToInt32(Properties["Shape"]);
			light.NetworkSpotAngle = Convert.ToSingle(Properties["SpotAngle"]);
			light.NetworkInnerSpotAngle = Convert.ToSingle(Properties["InnerSpotAngle"]);
			light.NetworkShadowStrength = Convert.ToSingle(Properties["ShadowStrength"]);
		}

		return light.gameObject;
	}

	private GameObject CreatePickup(SchematicObject schematicObject)
	{
		if (Properties.TryGetValue("Chance", out object property) && UnityEngine.Random.Range(0, 101) > Convert.ToSingle(property))
			return new("Empty Pickup");

		Pickup pickup = Pickup.Create((ItemType)Convert.ToInt32(Properties["ItemType"]), Vector3.zero)!;
		if (Properties.ContainsKey("Locked"))
			PickupEventsHandler.ButtonPickups.Add(pickup.Serial, schematicObject);

		return pickup.GameObject;
	}

	private GameObject CreateWorkstation()
	{
		WorkstationController workstation = GameObject.Instantiate(PrefabManager.Workstation);
		workstation.NetworkStatus = (byte)(Properties.TryGetValue("IsInteractable", out object isInteractable) && Convert.ToBoolean(isInteractable) ? 0 : 4);
		NetworkServer.UnSpawn(workstation.gameObject);
		return workstation.gameObject;
	}

	private GameObject CreateTeleport(Transform parentTransform)
	{
		GameObject gameObject = GameObject.Instantiate(new GameObject("Teleport"));
		gameObject.AddComponent<BoxCollider>().isTrigger = true;
		var teleport = gameObject.AddComponent<SchematicTeleportObject>();
		teleport.Cooldown = Convert.ToSingle(Properties["Cooldown"]);
		foreach (var target in (List<object>)Properties["Targets"])
		{
			teleport.Targets.Add(Convert.ToString(target));
		}

		teleport.Id = Name;
		return gameObject;
	}

	private GameObject CreateLocker()
	{
		Locker lockerPrefab = (LockerType)Convert.ToInt32(Properties["LockerType"]) switch
		{
			LockerType.PedestalScp500 => PrefabManager.PedestalScp500,
			LockerType.LargeGun => PrefabManager.LockerLargeGun,
			LockerType.RifleRack => PrefabManager.LockerRifleRack,
			LockerType.Misc => PrefabManager.LockerMisc,
			LockerType.Medkit => PrefabManager.LockerRegularMedkit,
			LockerType.Adrenaline => PrefabManager.LockerAdrenalineMedkit,
			LockerType.PedestalScp018 => PrefabManager.PedestalScp018,
			LockerType.PedestalScp207 => PrefabManager.PedstalScp207,
			LockerType.PedestalScp244 => PrefabManager.PedestalScp244,
			LockerType.PedestalScp268 => PrefabManager.PedestalScp268,
			LockerType.PedestalScp1853 => PrefabManager.PedstalScp1853,
			LockerType.PedestalScp2176 => PrefabManager.PedestalScp2176,
			LockerType.PedestalScpScp1576 => PrefabManager.PedestalScp1576,
			LockerType.PedestalAntiScp207 => PrefabManager.PedestalAntiScp207,
			LockerType.PedestalScp1344 => PrefabManager.PedestalScp1344,
			LockerType.ExperimentalWeapon => PrefabManager.LockerExperimentalWeapon,
			_ => throw new InvalidOperationException(),
		};
		Locker locker = GameObject.Instantiate(lockerPrefab);

		List<SerializableLockerChamber> convertedChambers = new(((List<object>)Properties["Chambers"]).Count);
		foreach (var json in (List<object>)Properties["Chambers"])
		{
			var chamber = Convert.ToString(json);
			convertedChambers.Add(JsonSerializer.Deserialize<SerializableLockerChamber>(chamber));
		}

		List<SerializableLockerLoot> convertedLoot = new(((List<object>)Properties["Loot"]).Count);
		foreach (var json in (List<object>)Properties["Loot"])
		{
			var loot = Convert.ToString(json);
			convertedLoot.Add(JsonSerializer.Deserialize<SerializableLockerLoot>(loot));
		}

		LabApiLocker labApiLocker = LabApiLocker.Get(locker);
		labApiLocker.ClearLockerLoot();
		foreach (var loot in convertedLoot)
		{
			labApiLocker.AddLockerLoot(loot.TargetItem, loot.RemainingUses, loot.ProbabilityPoints, loot.MinPerChamber,
				loot.MaxPerChamber);
		}

		int i = 0;
		labApiLocker.ClearAllChambers();
		foreach (LapApiLockerChamber chamber in labApiLocker.Chambers)
		{
			if (i > convertedChambers.Count - 1)
				break;

			chamber.AcceptableItems = convertedChambers[i].AcceptableItems.ToArray();
			chamber.RequiredPermissions = convertedChambers[i].RequiredPermissions;
			i++;
		}

		NetworkServer.UnSpawn(locker.gameObject);

		Timing.CallDelayed(0.25f, () =>
		{
			foreach (ItemPickupBase itemPickupBase in locker.GetComponentsInChildren<ItemPickupBase>())
			{
				if (itemPickupBase.TryGetComponent(out Rigidbody rigidbody))
					rigidbody.isKinematic = false;
			}

			i = 0;
			foreach (LapApiLockerChamber chamber in labApiLocker.Chambers)
			{
				chamber.IsOpen = convertedChambers[i].IsOpen;
				i++;
			}
		});

		return locker.gameObject;
	}

	private GameObject CreateDoor()
	{
		DoorVariant prefab = (DoorType)Convert.ToInt32(Properties["DoorType"]) switch
		{
			DoorType.Hcz or DoorType.HeavyContainmentDoor => PrefabManager.DoorHcz,
			DoorType.Bulkdoor or DoorType.HeavyBulkDoor => PrefabManager.DoorHeavyBulk,
			DoorType.Lcz or DoorType.LightContainmentDoor => PrefabManager.DoorLcz,
			DoorType.Ez or DoorType.EntranceDoor => PrefabManager.DoorEz,
			_ => PrefabManager.DoorEz
		};
		DoorVariant doorVariant = GameObject.Instantiate(prefab);
		if (doorVariant.TryGetComponent(out DoorRandomInitialStateExtension doorRandomInitialStateExtension))
			GameObject.Destroy(doorRandomInitialStateExtension);

		doorVariant.NetworkTargetState = Convert.ToBoolean(Properties["IsOpen"]);
		doorVariant.ServerChangeLock(DoorLockReason.SpecialDoorFeature, Convert.ToBoolean(Properties["IsLocked"]));
		doorVariant.RequiredPermissions = new DoorPermissionsPolicy(
			(DoorPermissionFlags)Convert.ToUInt16(Properties["RequiredPermissions"]),
			Convert.ToBoolean(Properties["RequireAll"]));
		return doorVariant.gameObject;
	}

	private GameObject CreateInteractable()
	{
		InvisibleInteractableToy interactableToy = GameObject.Instantiate(PrefabManager.InvisibleInteractableToy);
		interactableToy.NetworkMovementSmoothing = 60;
		interactableToy.NetworkShape = (InvisibleInteractableToy.ColliderShape)Convert.ToInt32(Properties["Shape"]);
		interactableToy.NetworkInteractionDuration = float.Parse(Properties["InteractionDuration"].ToString());
		interactableToy.NetworkIsLocked = bool.Parse(Properties["IsLocked"].ToString());

		return interactableToy.gameObject;
	}

	private GameObject CreateText()
	{
		TextToy textToy = GameObject.Instantiate(PrefabManager.Text);
		textToy.NetworkMovementSmoothing = 60;
		textToy.Network_textFormat = Convert.ToString(Properties["Text"]);
		return textToy.gameObject;
	}

	private GameObject CreateCamera()
	{
		Scp079CameraToy prefab = (CameraType)Convert.ToInt32(Properties["CameraType"]) switch
		{
			CameraType.Lcz => PrefabManager.CameraLcz,
			CameraType.Hcz => PrefabManager.CameraHcz,
			CameraType.Ez => PrefabManager.CameraEz,
			CameraType.EzArm => PrefabManager.CameraEzArm,
			CameraType.Sz => PrefabManager.CameraSz,
			_ => throw new InvalidOperationException(),
		};
		
		Scp079CameraToy cameraVariant = GameObject.Instantiate(prefab);
		cameraVariant.NetworkScale = Scale == Vector3.zero ? Vector3.one : Scale;
		cameraVariant.NetworkMovementSmoothing = 60;
		cameraVariant.Label = Convert.ToString(Properties["Label"]);
		cameraVariant.SetRoom(null, null);
		
		return cameraVariant.gameObject;
	}

	private GameObject CreateShootingTarget()
	{
		ShootingTarget prefab = (TargetType)Convert.ToInt32(Properties["TargetType"]) switch
		{
			TargetType.Binary => PrefabManager.ShootingTargetBinary,
			TargetType.ClassD => PrefabManager.ShootingTargetDBoy,
			TargetType.Sport => PrefabManager.ShootingTargetSport,
			_ => throw new InvalidOperationException(),
		};
		
		ShootingTarget shootingTarget = GameObject.Instantiate(prefab);
		return shootingTarget.gameObject;
	}

	private GameObject CreatePlayerSpawnPoint()
	{
		return CreateEmpty();
	}

	private GameObject CreateCapybara()
	{
		CapybaraToy capybaraToy = GameObject.Instantiate(PrefabManager.Capybara);
		capybaraToy.Network_collisionsEnabled = true;
		return capybaraToy.gameObject;
	}
}