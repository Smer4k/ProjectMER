using AdminToys;
using LabApi.Features.Wrappers;
using MapGeneration.Distributors;
using MEC;
using Mirror;
using ProjectMER.Events.Handlers;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace ProjectMER.Features.Serializable.Schematics;

public class SerializableSchematic : SerializableObject
{
	public string SchematicName { get; set; } = "None";

	public override GameObject? SpawnOrUpdateObject(Room? room = null, GameObject? instance = null)
	{
		if (Data == null)
			return null;

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
			NetworkServer.Spawn(schematic.gameObject);
			schematic.gameObject.AddComponent<SchematicObject>().Init(Data);
		}

		return schematic.gameObject;
	}

	public void UpdatePositionCustomObjects(GameObject instance)
	{
		if (Data == null)
			return;

		if (!instance.TryGetComponent(out SchematicObject schematicObject)) 
			return;
		
		foreach (var block in Data.Blocks)
		{
			if (block.BlockType is not BlockType.Workstation and not BlockType.Locker) continue;
			var gameObject = schematicObject.ObjectFromId[block.ObjectId].gameObject;
			if (gameObject.TryGetComponent(out StructurePositionSync structurePositionSync))
			{
				structurePositionSync.Network_position = gameObject.transform.position;
				structurePositionSync.Network_rotationY =
					(sbyte)Mathf.RoundToInt(gameObject.transform.rotation.eulerAngles.y / 5.625f);
			}
			NetworkServer.UnSpawn(gameObject);
			NetworkServer.Spawn(gameObject);
		}
	}
	
	public void UpdatePositionCustomObjects(PrimitiveObjectToy instance) => UpdatePositionCustomObjects(instance.gameObject);

	public SchematicObjectDataList? Data => _data ??= MapUtils.TryGetSchematicDataByName(SchematicName, out SchematicObjectDataList data) ? data : null;
	private SchematicObjectDataList? _data;
}
