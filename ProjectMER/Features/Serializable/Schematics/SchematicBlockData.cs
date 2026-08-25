using AdminToys;
using Footprinting;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Firearms.Attachments;
using LabApi.Features.Wrappers;
using MapGeneration;
using MapGeneration.Distributors;
using MEC;
using Mirror;
using PlayerRoles;
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
using Random = UnityEngine.Random;
using SpawnableCullingParent = AdminToys.SpawnableCullingParent;
using TextToy = AdminToys.TextToy;
using WaypointToy = AdminToys.WaypointToy;

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

	public GameObject? Create(SchematicObject schematicObject, Transform parentTransform)
	{
		var commonBlock = BlockType is BlockType.Light or BlockType.Empty or BlockType.Interactable
			or BlockType.Primitive or BlockType.Schematic or BlockType.Pickup or BlockType.Waypoint or BlockType.Text;

		if (!commonBlock && ProjectMER.Singleton.Config!.BackwardСompatibility)
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

		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
		if (Properties == null)
		{
			Properties = new();
		}

		GameObject? gameObject = BlockType switch
		{
			BlockType.Empty => CreateEmpty(schematicObject),
			BlockType.Primitive => CreatePrimitive(),
			BlockType.Light => CreateLight(schematicObject),
			BlockType.Pickup => CreatePickup(schematicObject),
			BlockType.Workstation => CreateWorkstation(),
			BlockType.Teleport => CreateTeleport(parentTransform),
			BlockType.Text => CreateText(),
			BlockType.Interactable => CreateInteractable(),
			BlockType.Waypoint => CreateWaypoint(),
			BlockType.Locker => CreateLocker(),
			BlockType.Door => CreateDoor(),
			BlockType.Camera => CreateCamera(),
			BlockType.ShootingTarget => CreateShootingTarget(),
			BlockType.PlayerSpawnPoint => CreatePlayerSpawnPoint(schematicObject, parentTransform),
			BlockType.Capybara => CreateCapybara(),
			BlockType.PlayerBlocker => CreatePlayerBlocker(),
			BlockType.CullingParent => CreateCullingParent(),
			BlockType.MirrorPrefab => CreateMirrorPrefab(),
			BlockType.Clutter => CreateClutter(),
			BlockType.Trigger => CreateTrigger(schematicObject),
			BlockType.AudioPlayer => CreateAudioPlayer(schematicObject),
			BlockType.CullingZone => CreateCullingZone(),
			BlockType.Generator => CreateGenerator(),
			BlockType.CameraTransfer => CreateCameraTransfer(schematicObject),
			_ => CreateEmpty(fallback: true)
		};
		
		if (gameObject == null)
			return null;
		gameObject.name = Name;

		Transform transform = gameObject.transform;
		transform.SetParent(parentTransform);
		transform.SetLocalPositionAndRotation(Position, Quaternion.Euler(Rotation));
		
		if (BlockType != BlockType.Waypoint)
		{
			transform.localScale = BlockType switch
			{
				BlockType.Empty when Scale == Vector3.zero => Vector3.one,
				_ => Scale,
			};
		}

		// if you don't remove the parent before NetworkServer.Spawn then there won't be a door
		if (BlockType is BlockType.Door or BlockType.CullingParent or BlockType.MirrorPrefab)
		{
			transform.SetParent(null);
		}
		
		if (gameObject.TryGetComponent(out AdminToyBase adminToyBase))
		{
			if (Properties != null)
			{
				if (Properties.TryGetValue("Static", out object isStatic) && Convert.ToBoolean(isStatic))
				{
					adminToyBase.NetworkIsStatic = true;
				} else if (Properties.TryGetValue("MovementSmoothing", out object movementSmoothing))
				{
					adminToyBase.NetworkMovementSmoothing = Convert.ToByte(movementSmoothing);
				} else
				{
					adminToyBase.NetworkMovementSmoothing = 60;
				}
			}

			if (adminToyBase is WaypointToy waypointToy)
			{
				waypointToy.BoundsSize = Scale;
			}
		}

		if (gameObject.TryGetComponent(out SpawnableCullingParent cullingParent))
		{
			cullingParent.NetworkBoundsPosition = gameObject.transform.position;
			if (Properties != null && Properties.TryGetValue("BoundsSize", out var value))
				cullingParent.NetworkBoundsSize = value.ToVector3();
			else
				cullingParent.NetworkBoundsSize = Scale;
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

	private GameObject CreateEmpty(SchematicObject? schematicObject = null, bool fallback = false)
	{
		if (fallback)
			Logger.Warn($"{BlockType} is not yet implemented. Object will be an empty GameObject instead.");

		PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);
		primitive.NetworkPrimitiveFlags = PrimitiveFlags.None;
		
		if (!Properties.TryGetValue("Damageable", out object damageableObj))
		{
			return primitive.gameObject;
		}
		
		var damageable = Convert.ToBoolean(damageableObj);
		if (!damageable)
		{
			return primitive.gameObject;
		}
		var damageableObject = primitive.gameObject.AddComponent<DamageableObject>();
		if (Properties.TryGetValue("Health", out object healthObj))
		{
			damageableObject.Health = Convert.ToSingle(healthObj);
		}

		if (Properties.TryGetValue("Weapons", out object weaponsObj))
		{
			foreach (var weapon in (List<object>)weaponsObj)
			{
				damageableObject.Weapons.Add((ItemType)Convert.ToInt32(weapon));
			}
		}

		if (Properties.TryGetValue("ExplosionTypes", out object explosionTypesObj))
		{
			damageableObject.ExplosionTypes.Clear();
			foreach (var role in (List<object>)explosionTypesObj)
			{
				damageableObject.ExplosionTypes.Add((ExplosionType)Convert.ToInt32(role));
			}
		}

		if (Properties.TryGetValue("Roles", out object rolesObj))
		{
			foreach (var role in (List<object>)rolesObj)
			{
				damageableObject.Roles.Add((RoleTypeId)Convert.ToSByte(role));
			}
		}

		damageableObject.SchematicObject = schematicObject;
		damageableObject.ObjectId = ObjectId;
		return primitive.gameObject;
	}

	private GameObject CreatePrimitive()
	{
		PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);

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

		if (Properties.TryGetValue("Scp106Passable", out object scp106PassableObj)
		    && Convert.ToBoolean(scp106PassableObj))
		{
			primitive.gameObject.AddComponent<Scp106PassableObject>();
		}
		
		return primitive.gameObject;
	}

	private GameObject CreateLight(SchematicObject schematicObject)
	{
		LightSourceToy light = GameObject.Instantiate(PrefabManager.LightSource);

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

		if (Properties.TryGetValue("Flicker", out object flickerEnable))
		{
			if (!Convert.ToBoolean(flickerEnable)) 
				return light.gameObject;
			var flicker = light.gameObject.AddComponent<FlickerController>();
			flicker.ObjectId = ObjectId;
			flicker.AddSchematic(schematicObject);
			if (Properties.TryGetValue("FlickerZone", out var obj))
			{
				flicker.Zone = (FacilityZone)Convert.ToInt32(obj);
			}

			if (Properties.TryGetValue("Cycle", out obj))
			{
				flicker.Cycle = Convert.ToBoolean(obj);
			}
			
			if (Properties.TryGetValue("RandomInRange", out obj))
			{
				flicker.RandomInRange = Convert.ToBoolean(obj);
			}

			if (flicker.RandomInRange)
			{
				if (Properties.TryGetValue("MaxOn", out obj))
				{
					flicker.MaxOn = Convert.ToSingle(obj);
				}

				if (Properties.TryGetValue("MinOn", out obj))
				{
					flicker.MinOn = Convert.ToSingle(obj);
				}

				if (Properties.TryGetValue("MaxOff", out obj))
				{
					flicker.MaxOff = Convert.ToSingle(obj);
				}

				if (Properties.TryGetValue("MinOff", out obj))
				{
					flicker.MinOff = Convert.ToSingle(obj);
				}
			}
			else
			{
				if (Properties.TryGetValue("TimeToOn", out obj))
				{
					flicker.TimeToOn = Convert.ToSingle(obj);
				}

				if (Properties.TryGetValue("TimeToOff", out obj))
				{
					flicker.TimeToOff = Convert.ToSingle(obj);
				}
			}
		}
		
		return light.gameObject;
	}

    private GameObject CreatePickup(SchematicObject schematicObject)
    {
        if (Properties.TryGetValue("Chance", out object property) &&
            UnityEngine.Random.Range(0, 101) > Convert.ToSingle(property))
            return new("Empty Pickup");

#if EXILED
        if (Properties.TryGetValue("CustomItem", out object customItemObj))
        {
            string customItemName = Convert.ToString(customItemObj);

            if (!string.IsNullOrWhiteSpace(customItemName) &&
                Exiled.CustomItems.API.Features.CustomItem.TryGet(customItemName, out var customItem))
            {
                var exiledPickup = customItem!.Spawn(Vector3.zero);

                if (exiledPickup != null)
                {
                    var labPickup = LabApi.Features.Wrappers.Pickup.Get(exiledPickup.Base);
                    return labPickup.GameObject;
                }
            }
        }
#endif


		//fb
        Pickup fallback = Pickup.Create(
            (ItemType)Convert.ToInt32(Properties["ItemType"]),
            Vector3.zero
        )!;

        if (Properties.ContainsKey("Locked"))
            PickupEventsHandler.ButtonPickups.Add(fallback.Serial, schematicObject);

        return fallback.GameObject;
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
			
			if (chamber.Base is PrecisionLockerChamber precisionLockerChamber)
			{
				if (precisionLockerChamber._spawnpointOverrides.Length > 1)
					chamber.Base.Spawnpoint = precisionLockerChamber._spawnpointOverrides[1].Spawnpoint;
				else if (precisionLockerChamber._spawnpointOverrides.Length == 1)
					chamber.Base.Spawnpoint = precisionLockerChamber._spawnpointOverrides[0].Spawnpoint;
			}
			
			chamber.AcceptableItems = convertedChambers[i].AcceptableItems.ToArray();
			chamber.RequiredPermissions = convertedChambers[i].RequiredPermissions;
			i++;
		}

		NetworkServer.UnSpawn(locker.gameObject);

		Timing.CallDelayed(0.25f, () =>
		{
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
			DoorType.Gate => PrefabManager.DoorGate,
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
		cameraVariant.NetworkLabel = Convert.ToString(Properties["Label"]);
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

	private GameObject CreatePlayerSpawnPoint(SchematicObject schematicObject,Transform parent)
	{
		var spawn = new GameObject("PlayerSpawnpoint");
		var component = spawn.AddComponent<SchematicPlayerSpawnpointObject>();
		foreach (var role in (List<object>)Properties["Roles"])
		{
			component.Roles.Add((RoleTypeId)Convert.ToSByte(role));
		}
		return spawn;
	}

	private GameObject CreateCapybara()
	{
		CapybaraToy capybaraToy = GameObject.Instantiate(PrefabManager.Capybara);
		capybaraToy.CollisionsEnabled = true;
		return capybaraToy.gameObject;
	}

	private GameObject CreateText()
	{
		TextToy text = GameObject.Instantiate(PrefabManager.Text);

		text.TextFormat = Convert.ToString(Properties["Text"]);
		text.DisplaySize = Properties["DisplaySize"].ToVector2() * 20f;

		return text.gameObject;
	}

	private GameObject CreateInteractable()
	{
		InvisibleInteractableToy interactable = GameObject.Instantiate(PrefabManager.Interactable);
		interactable.NetworkShape = (InvisibleInteractableToy.ColliderShape)Convert.ToInt32(Properties["Shape"]);
		interactable.NetworkInteractionDuration = Convert.ToSingle(Properties["InteractionDuration"]);
		interactable.NetworkIsLocked = Properties.TryGetValue("IsLocked", out object isLocked) && Convert.ToBoolean(isLocked);

		return interactable.gameObject;
	}

	private GameObject CreateWaypoint()
	{
		WaypointToy waypoint = GameObject.Instantiate(PrefabManager.Waypoint);
		return waypoint.gameObject;
	}

	private GameObject CreatePlayerBlocker()
	{
		PrimitiveObjectToy primitive = GameObject.Instantiate(PrefabManager.PrimitiveObject);
		var primitiveType = (PrimitiveType)Convert.ToInt32(Properties["PrimitiveType"]);
		
		primitive.NetworkPrimitiveType = primitiveType;
		primitive.PrimitiveFlags = PrimitiveFlags.Collidable;

		var itemsAllowed = true;
		var bulletsAllowed = true;
		if (Properties.TryGetValue("ItemsAllowed", out object itemsAllowedObj))
		{
			itemsAllowed = Convert.ToBoolean(itemsAllowedObj);
		}

		if (Properties.TryGetValue("BulletsAllowed", out object bulletsAllowedObj))
		{
			bulletsAllowed = Convert.ToBoolean(bulletsAllowedObj);
		}
		
		var playerBlocker = primitive.gameObject.AddComponent<PlayerBlockerObject>();
		
		if (Properties.TryGetValue("Roles", out object rolesObj))
		{
			foreach (var role in (List<object>)rolesObj)
			{
				playerBlocker.Roles.Add((RoleTypeId)Convert.ToSByte(role));
			}
		}
		
		playerBlocker.BulletsAllowed = bulletsAllowed;
		playerBlocker.ItemsAllowed = itemsAllowed;
		playerBlocker.UpdateState();
		
		return primitive.gameObject;
	}

	private GameObject CreateCullingParent()
	{
		var cullingParent = GameObject.Instantiate(PrefabManager.CullingParent);
		return cullingParent.gameObject;
	}

	private GameObject CreateMirrorPrefab()
	{
		var type = (MirrorPrefabType)Convert.ToInt32(Properties["MirrorType"]);
		var prefab = PrefabManager.GetMirrorPrefab(type);
		return GameObject.Instantiate(prefab);
	}

	private GameObject? CreateClutter()
	{
		var chance = Convert.ToSingle(Properties["SpawnChance"]);
		return Random.Range(0f, 100f) > chance ? null : CreateEmpty();
	}

	public GameObject? CreateTrigger(SchematicObject schematicObject)
	{
		GameObject gameObject = GameObject.Instantiate(new GameObject("Trigger"));
		var primitiveType = (PrimitiveType)Convert.ToInt32(Properties["PrimitiveType"]);
		Collider collider;
		switch (primitiveType)
		{
			case PrimitiveType.Sphere:
				collider = gameObject.AddComponent<SphereCollider>();
				break;
			case PrimitiveType.Capsule:
			case PrimitiveType.Cylinder:
				var capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
				capsuleCollider.radius = 0.5f;
				capsuleCollider.height = 2f;
				collider = capsuleCollider;
				break;
			default:
				collider = gameObject.AddComponent<BoxCollider>();
				break;
		}
		
		collider.isTrigger = true;
		gameObject.AddComponent<Rigidbody>().isKinematic = true;
		var triggerObject = gameObject.AddComponent<TriggerObject>();
		triggerObject.SchematicObject = schematicObject;
		triggerObject.ObjectId = ObjectId;
		
		if (Properties.TryGetValue("TargetType", out object targetType))
		{
			triggerObject.TargetType = (TriggerTargetType)Convert.ToInt32(targetType);
		}
		
		return gameObject;
	}

	public GameObject? CreateAudioPlayer(SchematicObject schematicObject)
	{
		GameObject gameObject = CreateEmpty();
		var settings = new AudioPlayerSettings();
		
		if (Properties.TryGetValue("FileName", out object fileNameObj))
			settings.FileName = Convert.ToString(fileNameObj);
		
		if (Properties.TryGetValue("IsShortClip", out object isShortClipObj))
			settings.IsShortClip = Convert.ToBoolean(isShortClipObj);
		
		if (Properties.TryGetValue("PlayOnSpawn", out object playOnSpawnObj))
			settings.PlayOnSpawn = Convert.ToBoolean(playOnSpawnObj);
		
		if (Properties.TryGetValue("Loop", out object loopObj))
			settings.Loop = Convert.ToBoolean(loopObj);

		if (Properties.TryGetValue("IsSpatial", out object isSpatialObj))
			settings.IsSpatial = Convert.ToBoolean(isSpatialObj);

		if (Properties.TryGetValue("Volume", out object volumeObj))
			settings.Volume = Convert.ToSingle(volumeObj);
		
		if (Properties.TryGetValue("MinDistance", out object minDistanceObj))
			settings.MinDistance = Convert.ToSingle(minDistanceObj);

		if (Properties.TryGetValue("MaxDistance", out object maxDistanceObj))
			settings.MaxDistance = Convert.ToSingle(maxDistanceObj);
		
		if (Properties.TryGetValue("Speed", out object speedObj))
			settings.Speed = Convert.ToSingle(speedObj);
		
		schematicObject.AudioPlayerSettingsByObjectId.Add(ObjectId, settings);
		return gameObject;
	}

	public GameObject? CreateCullingZone()
	{
		var empty = CreateEmpty();
		var cullingZoneObject = empty.AddComponent<CullingZoneObject>();
		empty.layer = 29; // fence
		
		if (Properties.TryGetValue("ObjectPerSpawn", out object numberOfObjectPerSpawnObj))
			cullingZoneObject.NumberOfObjectPerSpawn = Convert.ToInt32(numberOfObjectPerSpawnObj);

		var colliderShape = InvisibleInteractableToy.ColliderShape.Sphere;
		if (Properties.TryGetValue("ColliderShape", out object colliderShapeObj))
			colliderShape = (InvisibleInteractableToy.ColliderShape)Convert.ToInt32(colliderShapeObj);

		var colliderSize = Vector3.one;
		if (Properties.TryGetValue("ColliderSize", out object colliderSizeObj))
		{
			colliderSize = colliderSizeObj.ToVector3();
		}
		
		switch (colliderShape)
		{
			case InvisibleInteractableToy.ColliderShape.Sphere:
				var sphereCollider = cullingZoneObject.gameObject.AddComponent<SphereCollider>();
				sphereCollider.isTrigger = true;
				sphereCollider.radius = colliderSize.x;
				break;
			case InvisibleInteractableToy.ColliderShape.Box:
				var boxCollider = cullingZoneObject.gameObject.AddComponent<BoxCollider>();
				boxCollider.isTrigger = true;
				boxCollider.size = colliderSize;
				if (Properties.TryGetValue("ColliderCenter", out object colliderCenterObj))
					boxCollider.center = colliderCenterObj.ToVector3();
				break;
			case InvisibleInteractableToy.ColliderShape.Capsule:
				var capsuleCollider = cullingZoneObject.gameObject.AddComponent<CapsuleCollider>();
				capsuleCollider.isTrigger = true;
				capsuleCollider.height = colliderSize.y;
				capsuleCollider.radius = colliderSize.x;
				break;
			default:
				sphereCollider = cullingZoneObject.gameObject.AddComponent<SphereCollider>();
				sphereCollider.isTrigger = true;
				sphereCollider.radius = colliderSize.x;
				break;
		}
		
		return empty;
	}

	public GameObject? CreateGenerator()
	{
		var generator = GameObject.Instantiate(PrefabManager.Generator);
		Scp079Generator.GeneratorFlags flags = Scp079Generator.GeneratorFlags.None;
		
		if (Properties.TryGetValue("GeneratorFlags", out object generatorFlagsObj))
		{
			flags = (Scp079Generator.GeneratorFlags)Convert.ToByte(generatorFlagsObj);
		}

		if (flags.HasFlag(Scp079Generator.GeneratorFlags.Unlocked))
		{
			generator.IsUnlocked = true;
		}

		if (flags.HasFlag(Scp079Generator.GeneratorFlags.Open))
		{
			generator.IsOpen = true;
		}

		if (flags.HasFlag(Scp079Generator.GeneratorFlags.Activating))
		{
			generator.Activating = true;
			generator._leverStopwatch.Restart();
			generator._lastActivator = new Footprint();
		}

		if (flags.HasFlag(Scp079Generator.GeneratorFlags.Engaged))
		{
			generator.Engaged = true;
		}

		if (Properties.TryGetValue("RequiredPermissions", out object requiredPermissionsObj))
		{
			generator.RequiredPermissions = (DoorPermissionFlags)Convert.ToUInt16(requiredPermissionsObj);
		}

		if (Properties.TryGetValue("TotalActivationTime", out object totalActivationTimeObj))
		{
			generator.TotalActivationTime = Convert.ToSingle(totalActivationTimeObj);
		}

		if (Properties.TryGetValue("TotalDeactivationTime", out object totalDeactivationTimeObj))
		{
			generator.TotalDeactivationTime = Convert.ToSingle(totalDeactivationTimeObj);
		}
		
		return generator.gameObject;
	}

	public GameObject? CreateCameraTransfer(SchematicObject schematicObject)
	{
		Scp079CameraToy prefab;
		if (Properties.TryGetValue("CameraType", out object cameraObj))
		{
			prefab = (CameraType)Convert.ToInt32(cameraObj) switch
			{
				CameraType.Lcz => PrefabManager.CameraLcz,
				CameraType.Hcz => PrefabManager.CameraHcz,
				CameraType.Ez => PrefabManager.CameraEz,
				CameraType.EzArm => PrefabManager.CameraEzArm,
				CameraType.Sz => PrefabManager.CameraSz,
				_ => throw new InvalidOperationException(),
			};
		}
		else
		{
			prefab = PrefabManager.CameraLcz;
		}
		
		var camera = GameObject.Instantiate(prefab);
		Scale = new Vector3(0.01f, 0.01f, 0.01f);

		if (Properties.TryGetValue("TargetCamera", out object targetCameraObj))
		{
			Timing.CallDelayed(0.2f, () =>
			{
				var id = Convert.ToInt32(targetCameraObj);
				if (!schematicObject.ObjectFromId.TryGetValue(id, out var target) ||
				    !target.TryGetComponent<Scp079CameraToy>(out var targetCamera))
					return;
				camera.gameObject.AddComponent<CameraTransferObject>().Init(targetCamera.Camera);
				camera.NetworkLabel = targetCamera.NetworkLabel;
			});
		}

		return camera.gameObject;
	}
}
