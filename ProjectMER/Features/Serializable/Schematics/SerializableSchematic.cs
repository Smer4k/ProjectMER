using AdminToys;
using LabApi.Features.Wrappers;
using MapGeneration.Distributors;
using Mirror;
using ProjectMER.Events.Handlers;
using ProjectMER.Features.Enums;
using ProjectMER.Events.Arguments;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Interfaces;
using ProjectMER.Features.Objects;
using RelativePositioning;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using SpawnableCullingParent = AdminToys.SpawnableCullingParent;

namespace ProjectMER.Features.Serializable.Schematics;

public class SerializableSchematic : SerializableObject, IIndicatorDefinition
{
	// When provided, this folder will be used instead of the default ProjectMER.SchematicsDir
	public string? FolderPath { get; init; }
	public string SchematicName { get; set; } = "None";

	public override GameObject? SpawnOrUpdateObject(Room? room = null, GameObject? instance = null)
	{
		PrimitiveObjectToy schematic = instance == null ? UnityEngine.Object.Instantiate(PrefabManager.PrimitiveObject) : instance.GetComponent<PrimitiveObjectToy>();
		schematic.NetworkPrimitiveFlags = PrimitiveFlags.None;
		schematic.NetworkMovementSmoothing = 60;

		Vector3 position = room.GetAbsolutePosition(Position);
		Quaternion rotation = room.GetAbsoluteRotation(Rotation);
		_prevIndex = Index;

		schematic.name = $"CustomSchematic-{SchematicName}";
		schematic.transform.SetPositionAndRotation(position, rotation);
		schematic.transform.localScale = Scale;

		FlickerController.OnSchematicUpdate(schematic.gameObject);
		UpdatePositionCustomObjects(schematic);
		
		if (instance == null)
		{
			SchematicObjectDataList data;
			bool success;
			if (FolderPath != null)
			{
				success = MapUtils.TryGetSchematicDataByName(FolderPath, SchematicName, out data);
			}
			else
			{
				success = MapUtils.TryGetSchematicDataByName(SchematicName, out data);
			}

			if (!success)
			{
				GameObject.Destroy(schematic.gameObject);
				return null;
			}

			SchematicSpawningEventArgs ev = new(data, SchematicName);
			Schematic.OnSchematicSpawning(ev);
			data = ev.Data;

			if (!ev.IsAllowed)
			{
				GameObject.Destroy(schematic.gameObject);
				return null;
			}
			
			NetworkServer.Spawn(schematic.gameObject);
			schematic.gameObject.AddComponent<SchematicObject>().Init(data);
		}

		return schematic.gameObject;
	}

	public void UpdatePositionCustomObjects(GameObject instance, bool updateDoors = true)
	{
		SchematicObjectDataList data;
		if (FolderPath != null)
		{
			if (!MapUtils.TryGetSchematicDataByName(FolderPath, SchematicName, out data))
				return;
		}
		else
		{
			if (!MapUtils.TryGetSchematicDataByName(SchematicName, out data))
				return;
		}

		if (!instance.TryGetComponent(out SchematicObject schematicObject)) 
			return;
		
		foreach (var block in data.Blocks)
		{
			if (block.BlockType is not 
			    BlockType.Workstation and not 
			    BlockType.Locker and not
			    BlockType.Door and not 
			    BlockType.CullingParent and not
			    BlockType.MirrorPrefab) 
				continue;
			var gameObject = schematicObject.ObjectFromId[block.ObjectId].gameObject;
			
			if (block.BlockType == BlockType.Door && updateDoors)
			{
				var parent = schematicObject.ObjectFromId[block.ParentId].gameObject;
				gameObject.transform.SetParent(parent.transform);
				gameObject.transform.localPosition = block.Position;
				gameObject.transform.localScale = block.Scale;
				gameObject.transform.SetParent(null);
				if (gameObject.TryGetComponent(out NetIdWaypoint waypointBase))
				{
					waypointBase.SetPosition();
				}
			}

			if (block.BlockType == BlockType.MirrorPrefab)
			{
				var parent = schematicObject.ObjectFromId[block.ParentId].gameObject;
				gameObject.transform.SetParent(parent.transform);
				gameObject.transform.localPosition = block.Position;
				gameObject.transform.localScale = block.Scale;
				gameObject.transform.SetParent(null);
			}

			if (gameObject.TryGetComponent(out StructurePositionSync structurePositionSync))
			{
				structurePositionSync.Network_position = gameObject.transform.position;
				structurePositionSync.Network_rotationY =
					(sbyte)Mathf.RoundToInt(gameObject.transform.rotation.eulerAngles.y / 5.625f);
			}

			if (gameObject.TryGetComponent(out SpawnableCullingParent spawnableCullingParent))
			{
				var parent = schematicObject.ObjectFromId[block.ParentId].gameObject;
				gameObject.transform.SetParent(parent.transform);
				gameObject.transform.localPosition = block.Position;
				gameObject.transform.localScale = block.Scale;
				gameObject.transform.SetParent(null);
				spawnableCullingParent.NetworkBoundsPosition = gameObject.transform.position;
				spawnableCullingParent.NetworkBoundsSize = gameObject.transform.localScale;
			}
			
			if (block.BlockType == BlockType.Door && !updateDoors)
				continue;
			if (block.BlockType == BlockType.CullingParent)
				continue;
			NetworkServer.UnSpawn(gameObject);
			NetworkServer.Spawn(gameObject);
		}
	}
	
	public void UpdatePositionCustomObjects(PrimitiveObjectToy instance) => UpdatePositionCustomObjects(instance.gameObject);
	
	// This is necessary so that the schematic can be removed using a toolgun.
	public GameObject SpawnOrUpdateIndicator(Room room, GameObject? instance = null)
	{
		PrimitiveObjectToy root;
		Vector3 position = room.GetAbsolutePosition(Position);
		Quaternion rotation = room.GetAbsoluteRotation(Rotation);

		if (instance == null)
		{
			root = UnityEngine.Object.Instantiate(PrefabManager.PrimitiveObject);
			root.NetworkPrimitiveFlags = PrimitiveFlags.Visible;
			root.NetworkMaterialColor = new Color(2, 0, 0, 0.9f);
			root.NetworkPrimitiveType = PrimitiveType.Cube;
		}
		else
		{
			root = instance.GetComponent<PrimitiveObjectToy>();
		}
		
		root.transform.position = position;
		root.transform.rotation = rotation;
		root.transform.localScale = Scale;

		return root.gameObject;
	}
}
