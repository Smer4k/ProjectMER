using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Interfaces;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

namespace ProjectMER.Features.Serializable;

public class SerializablePlayerBlocker : SerializableObject, IIndicatorDefinition
{
    /// <summary>
    /// Gets or sets the <see cref="UnityEngine.PrimitiveType"/>.
    /// </summary>
    public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.Cube;
    
    public override GameObject SpawnOrUpdateObject(Room? room = null, GameObject? instance = null)
    {
        PrimitiveObjectToy primitive = instance == null ? UnityEngine.Object.Instantiate(PrefabManager.PrimitiveObject) : instance.GetComponent<PrimitiveObjectToy>();
        Vector3 position = room.GetAbsolutePosition(Position);
        Quaternion rotation = room.GetAbsoluteRotation(Rotation);
        _prevIndex = Index;

        primitive.transform.SetPositionAndRotation(position, rotation);
        primitive.transform.localScale = Scale;
        primitive.NetworkMovementSmoothing = 60;

        primitive.NetworkPrimitiveType = PrimitiveType;
        primitive.gameObject.layer = LayerMask.NameToLayer("InvisibleCollider");
        primitive.NetworkPrimitiveFlags = PrimitiveFlags.Collidable;
        
        if (instance == null)
            NetworkServer.Spawn(primitive.gameObject);

        return primitive.gameObject;
    }

    public GameObject SpawnOrUpdateIndicator(Room room, GameObject? instance = null)
    {
	    PrimitiveObjectToy root;
		Vector3 position = room.GetAbsolutePosition(Position);
		Quaternion rotation = room.GetAbsoluteRotation(Rotation);

		if (instance == null)
		{
			root = UnityEngine.Object.Instantiate(PrefabManager.PrimitiveObject);
			root.NetworkPrimitiveFlags = PrimitiveFlags.Visible;
			root.NetworkMaterialColor = new Color(1, 0, 0, 0.5f);
		}
		else
		{
			root = instance.GetComponent<PrimitiveObjectToy>();
		}
		
		root.NetworkPrimitiveType = PrimitiveType;
		root.transform.position = position;
		root.transform.rotation = rotation;
		root.transform.localScale = Scale;

		return root.gameObject;
    }
}