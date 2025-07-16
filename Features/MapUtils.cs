using Interactables.Interobjects.DoorUtils;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using PlayerRoles;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using ProjectMER.Features.Serializable.Lockers;
using ProjectMER.Features.Serializable.Schematics;
using ProjectMER.Features.ToolGun;
using UnityEngine;
using Utf8Json;
using YamlDotNet.Core;
using CameraType = ProjectMER.Features.Enums.CameraType;

namespace ProjectMER.Features;

public static class MapUtils
{
	public const string UntitledMapName = "Untitled";

	public static MapSchematic UntitledMap => LoadedMaps.GetOrAdd(UntitledMapName, () => new(UntitledMapName));

	public static Dictionary<string, MapSchematic> LoadedMaps { get; private set; } = [];

	public static void SaveMap(string mapName)
	{
		if (mapName == UntitledMapName)
			throw new InvalidOperationException("This map name is reserved for internal use!");
		
		foreach (var mapObject in UntitledMap.SpawnedObjects.ToArray())
		{
			if (mapObject.Base is not SerializableSchematic schematic) continue;
			if (schematic.Data == null) continue;
			foreach (var block in schematic.Data.Blocks.ToArray())
			{
				if (block.BlockType is BlockType.Light or BlockType.Empty or BlockType.Interactable or BlockType.Primitive or BlockType.Schematic or BlockType.Pickup) continue;
				var position = mapObject.GetComponent<SchematicObject>().ObjectFromId[block.ObjectId].position;
				Room room = RoomExtensions.GetRoomAtPosition(position);

				position = room.Name == RoomName.Outside ? position : room.Transform.InverseTransformPoint(position);
				string roomId = room.GetRoomStringId();

				ToolGunObjectType type = block.BlockType switch
				{
					BlockType.Teleport => ToolGunObjectType.Teleport,
					BlockType.Door => ToolGunObjectType.Door,
					BlockType.Locker => ToolGunObjectType.Locker,
					BlockType.Workstation => ToolGunObjectType.Workstation,
					BlockType.Text => ToolGunObjectType.Text,
					BlockType.Camera => ToolGunObjectType.Scp079Camera,
					BlockType.ShootingTarget => ToolGunObjectType.ShootingTarget,
					BlockType.PlayerSpawnPoint => ToolGunObjectType.PlayerSpawnpoint,
					BlockType.Capybara => ToolGunObjectType.Capybara,
					_ => throw new ArgumentOutOfRangeException()
				};

				SerializableObject serializableObject =
					(SerializableObject)Activator.CreateInstance(ToolGunItem.TypesDictionary[type]);
				serializableObject.Room = roomId;
				serializableObject.Index = room.GetRoomIndex();
				serializableObject.Position = position;
				serializableObject.Scale = block.Scale == Vector3.zero ? Vector3.one : block.Scale;
				serializableObject.Rotation = block.Rotation;

				var id = block.Name;
				
				switch (serializableObject)
				{
					case SerializableTeleport serializableTeleport:
					{
						if (block.Properties.TryGetValue("Targets", out var targetsObj))
						{
							foreach (var target in (List<object>)targetsObj)
							{
								serializableTeleport.Targets.Add(Convert.ToString(target));
							}
						}
						serializableTeleport.Cooldown = Convert.ToSingle(block.Properties["Cooldown"]);
						break;
					}
					case SerializableDoor serializableDoor:
						serializableDoor.DoorType = (DoorType)Convert.ToInt32(block.Properties["DoorType"]);
						serializableDoor.IsOpen = Convert.ToBoolean(block.Properties["IsOpen"]);
						serializableDoor.IsLocked = Convert.ToBoolean(block.Properties["IsLocked"]);
						serializableDoor.RequiredPermissions =
							(DoorPermissionFlags)Convert.ToUInt16(block.Properties["RequiredPermissions"]);
						serializableDoor.RequireAll = Convert.ToBoolean(block.Properties["RequireAll"]);
						break;
					case SerializableWorkstation serializableWorkstation:
						serializableWorkstation.IsInteractable = Convert.ToBoolean(block.Properties["IsInteractable"]);
						break;
					case SerializableLocker serializableLocker:
						List<SerializableLockerChamber> convertedChambers = new(((List<object>)block.Properties["Chambers"]).Count);
						foreach (var json in (List<object>)block.Properties["Chambers"])
						{
							var chamber = Convert.ToString(json);
							convertedChambers.Add(JsonSerializer.Deserialize<SerializableLockerChamber>(chamber));
						}
		
						List<SerializableLockerLoot> convertedLoot = new(((List<object>)block.Properties["Loot"]).Count);
						foreach (var json in (List<object>)block.Properties["Loot"])
						{
							var loot = Convert.ToString(json);
							convertedLoot.Add(JsonSerializer.Deserialize<SerializableLockerLoot>(loot));
						}
						serializableLocker.Loot = convertedLoot;
						serializableLocker.Chambers = convertedChambers;
						serializableLocker.LockerType = (LockerType)Convert.ToInt32(block.Properties["LockerType"]);
						break;
					case SerializableText serializableText:
						serializableText.Text = Convert.ToString(block.Properties["Text"]);
						break;
					case SerializableScp079Camera serializableScp079Camera:
						serializableScp079Camera.CameraType = (CameraType)Convert.ToInt32(block.Properties["CameraType"]);
						serializableScp079Camera.Label = Convert.ToString(block.Properties["Label"]);
						break;
					case SerializableShootingTarget serializableShootingTarget:
						serializableShootingTarget.TargetType = (TargetType)Convert.ToInt32(block.Properties["TargetType"]);
						break;
					case SerializablePlayerSpawnpoint serializablePlayerSpawnpoint:
						foreach (var role in (List<object>)block.Properties["Roles"])
						{
							serializablePlayerSpawnpoint.Roles.Add((RoleTypeId)Convert.ToSByte(role));
						}
						break;
				}

				if (UntitledMap.TryAddElement(id, serializableObject))
					UntitledMap.SpawnObject(id, serializableObject);
				
				foreach (MapEditorObject mapEditorObject in UntitledMap.SpawnedObjects.ToArray())
				{
					if (mapEditorObject.Id != id)
						continue;

					IndicatorObject.TrySpawnOrUpdateIndicator(mapEditorObject);
				}
			}
		}

		if (LoadedMaps.TryGetValue(mapName, out MapSchematic map)) // Map is already loaded
		{
			map.Merge(UntitledMap);
		}
		else if (TryGetMapData(mapName, out map)) // Map isn't loaded but map file exists
		{
			map.Merge(UntitledMap);
		}
		else // Map isn't loaded and map file doesn't exist
		{
			map = new MapSchematic(mapName).Merge(UntitledMap);
		}

		string path = Path.Combine(ProjectMER.MapsDir, $"{mapName}.yml");
		File.WriteAllText(path, YamlParser.Serializer.Serialize(map));
		map.IsDirty = false;

		UnloadMap(UntitledMapName);
		Timing.CallDelayed(1f, () => {LoadMap(mapName);});
	}

	public static void LoadMap(string mapName)
	{
		MapSchematic map = GetMapData(mapName);
		UnloadMap(mapName);
		map.Reload();

		LoadedMaps.Add(mapName, map);
	}

	public static bool UnloadMap(string mapName)
	{
		if (!LoadedMaps.ContainsKey(mapName))
			return false;

		foreach (MapEditorObject mapEditorObject in LoadedMaps[mapName].SpawnedObjects)
			mapEditorObject.Destroy();

		LoadedMaps.Remove(mapName);
		return true;
	}

	public static bool TryGetMapData(string mapName, out MapSchematic mapSchematic)
	{
		try
		{
			mapSchematic = GetMapData(mapName);
			return true;
		}
		catch (Exception)
		{
			mapSchematic = null!;
			return false;
		}
	}

	public static MapSchematic GetMapData(string mapName)
	{
		MapSchematic map;

		string path = Path.Combine(ProjectMER.MapsDir, $"{mapName}.yml");
		if (!File.Exists(path))
		{
			string error = $"Failed to load map data: File {mapName}.yml does not exist!";
			throw new FileNotFoundException(error);
		}

		try
		{
			map = YamlParser.Deserializer.Deserialize<MapSchematic>(File.ReadAllText(path));
			map.Name = mapName;
		}
		catch (YamlException e)
		{
			string error = $"Failed to load map data: File {mapName}.yml has YAML errors!\n{e.ToString().Split('\n')[0]}";
			throw new YamlException(error);
		}

		return map;
	}

	public static bool TryGetSchematicDataByName(string schematicName, out SchematicObjectDataList data)
	{
		try
		{
			data = GetSchematicDataByName(schematicName);
			return true;
		}
		catch (Exception)
		{
			data = null!;
			return false;
		}
	}

	public static SchematicObjectDataList GetSchematicDataByName(string schematicName)
	{
		SchematicObjectDataList data;
		string schematicDirPath = Path.Combine(ProjectMER.SchematicsDir, schematicName);
		string schematicJsonPath = Path.Combine(schematicDirPath, $"{schematicName}.json");
		string misplacedSchematicJsonPath = schematicDirPath + ".json";

		if (!Directory.Exists(schematicDirPath))
		{
			// Some users may throw a single JSON file into Schematics folder, this automatically creates and moved the file to the correct schematic directory.
			if (File.Exists(misplacedSchematicJsonPath))
			{
				Directory.CreateDirectory(schematicDirPath);
				File.Move(misplacedSchematicJsonPath, schematicJsonPath);
				return GetSchematicDataByName(schematicName);
			}

			string error = $"Failed to load schematic data: Directory {schematicName} does not exist!";
			Logger.Error(error);
			throw new DirectoryNotFoundException(error);
		}

		if (!File.Exists(schematicJsonPath))
		{
			// Same as above but with the folder existing and file not being there for some reason.
			if (File.Exists(misplacedSchematicJsonPath))
			{
				File.Move(misplacedSchematicJsonPath, schematicJsonPath);
				return GetSchematicDataByName(schematicName);
			}

			string error = $"Failed to load schematic data: File {schematicName}.json does not exist!";
			Logger.Error(error);
			throw new FileNotFoundException(error);
		}

		try
		{
			data = JsonSerializer.Deserialize<SchematicObjectDataList>(File.ReadAllText(schematicJsonPath));
			data.Path = schematicDirPath;
		}
		catch (JsonParsingException e)
		{
			string error = $"Failed to load schematic data: File {schematicName}.json has JSON errors!\n{e.ToString().Split('\n')[0]}";
			Logger.Error(error);
			throw new JsonParsingException(error);
		}

		return data;
	}

	public static string[] GetAvailableSchematicNames() => Directory.GetFiles(ProjectMER.SchematicsDir, "*.json", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension).Where(x => !x.Contains('-')).ToArray();

	public static string GetColoredMapName(string mapName)
	{
		if (mapName == UntitledMapName)
			return $"<color=grey><b><i>{UntitledMapName}</i></b></color>";

		bool isDirty = false;
		if (LoadedMaps.TryGetValue(mapName, out MapSchematic mapSchematic))
			isDirty = mapSchematic.IsDirty;

		return isDirty ? $"<i>{GetColoredString(mapName)}</i>" : GetColoredString(mapName);
	}

	public static string GetColoredString(string s)
	{
		uint value = Math.Min(((uint)s.GetHashCode()) / 255, 16777215);
		string colorHex = value.ToString("X6");
		return $"<color=#{colorHex}><b>{s}</b></color>";
	}
}
