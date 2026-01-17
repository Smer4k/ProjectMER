using AdminToys;
using LabApi.Features.Wrappers;
using MapGeneration.Distributors;
using Mirror;
using ProjectMER.Events.Handlers;
using ProjectMER.Features.Enums;
using ProjectMER.Events.Arguments;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using RelativePositioning;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace ProjectMER.Features.Serializable.Schematics;

public class SerializableSchematic : SerializableObject
{
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

		UpdatePositionCustomObjects(schematic);
		
		if (instance == null)
		{
			_ = MapUtils.TryGetSchematicDataByName(SchematicName, out SchematicObjectDataList? data) ? data : null;

			if (data == null)
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
		
		if (!MapUtils.TryGetSchematicDataByName(SchematicName, out SchematicObjectDataList data))
			return;

		if (!instance.TryGetComponent(out SchematicObject schematicObject)) 
			return;
		
		foreach (var block in data.Blocks)
		{
			if (block.BlockType is not 
			    BlockType.Workstation and not 
			    BlockType.Locker and not
			    BlockType.Door) 
				continue;
			var gameObject = schematicObject.ObjectFromId[block.ObjectId].gameObject;
			
			if (block.BlockType == BlockType.Door && updateDoors)
			{
				var parent = schematicObject.ObjectFromId[block.ParentId].gameObject;
				gameObject.transform.SetParent(parent.transform);
				gameObject.transform.localPosition = block.Position;
				gameObject.transform.SetParent(null);
				if (gameObject.TryGetComponent(out NetIdWaypoint waypointBase))
				{
					waypointBase.SetPosition();
				}
			}

			if (gameObject.TryGetComponent(out StructurePositionSync structurePositionSync))
			{
				structurePositionSync.Network_position = gameObject.transform.position;
				structurePositionSync.Network_rotationY =
					(sbyte)Mathf.RoundToInt(gameObject.transform.rotation.eulerAngles.y / 5.625f);
			}
			
			if (block.BlockType == BlockType.Door && !updateDoors)
				continue;
			NetworkServer.UnSpawn(gameObject);
			NetworkServer.Spawn(gameObject);
		}
	}
	
	public void UpdatePositionCustomObjects(PrimitiveObjectToy instance) => UpdatePositionCustomObjects(instance.gameObject);
}
