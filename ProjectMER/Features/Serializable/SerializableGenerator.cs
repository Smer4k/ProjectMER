using Footprinting;
using Interactables.Interobjects.DoorUtils;
using LabApi.Features.Wrappers;
using MapGeneration.Distributors;
using Mirror;
using ProjectMER.Features.Extensions;
using UnityEngine;

namespace ProjectMER.Features.Serializable;

public sealed class SerializableGenerator : SerializableObject
{
    public float TotalActivationTime { get; set; } = 125f;
    public float TotalDeactivationTime { get; set; } = 125f;
    public bool IsOpen { get; set; }
    public bool IsUnlocked { get; set; }
    public bool Engaged { get; set; }
    public bool Activating { get; set; }
    public DoorPermissionFlags RequiredPermissions { get; set; } = DoorPermissionFlags.ArmoryLevelTwo;
    public float DropdownSpeed { get; private set; } = 0;
    
    public override GameObject? SpawnOrUpdateObject(Room? room = null, GameObject? instance = null)
    {
        Scp079Generator generator;
        Vector3 position = room.GetAbsolutePosition(Position);
        Quaternion rotation = room.GetAbsoluteRotation(Rotation);
        _prevIndex = Index;
        
        if (instance == null)
        {
            generator = GameObject.Instantiate(PrefabManager.Generator);
        }
        else
        {
            generator = instance.GetComponent<Scp079Generator>();
        }
        generator.transform.SetPositionAndRotation(position, rotation);
        generator.transform.localScale = Scale;
        
        if (generator.TryGetComponent(out StructurePositionSync structurePositionSync))
        {
            structurePositionSync.Network_position = generator.transform.position;
            structurePositionSync.Network_rotationY = (sbyte)Mathf.RoundToInt(generator.transform.rotation.eulerAngles.y / 5.625f);
        }
        
        TotalActivationTime = Mathf.Max(0, TotalActivationTime);
        TotalDeactivationTime = Mathf.Max(0, TotalDeactivationTime);
        
        SetupGenerator(generator);
        
        NetworkServer.UnSpawn(generator.gameObject);
        NetworkServer.Spawn(generator.gameObject);
        return generator.gameObject;
    }

    public void SetupGenerator(Scp079Generator generator)
    {
        if (TotalActivationTime > 0 || TotalDeactivationTime > 0)
            DropdownSpeed = TotalActivationTime / TotalDeactivationTime;
        generator.TotalActivationTime = TotalActivationTime;
        generator.TotalDeactivationTime = TotalDeactivationTime;
        generator.IsOpen = IsOpen;
        generator.IsUnlocked = IsUnlocked;
        generator.Engaged = Engaged;
        generator.RequiredPermissions = RequiredPermissions;

        generator.Activating = Activating;
        if (Activating)
        {
            generator._leverStopwatch.Restart();
        }
        generator._lastActivator = new Footprint();
    }
}