using Interactables.Interobjects.DoorUtils;
using LabApi.Features.Wrappers;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;

namespace ProjectMER.Features.Actions;

public sealed class ActionInteractableToy
{
    public static readonly Dictionary<InteractableToy, ActionInteractableToy> Instances = new();

    public readonly InteractableToy Toy;
    public readonly SchematicObject SchematicObject;
    public readonly SchematicBlockData BlockData;

    public DoorPermissionFlags Permissions;
    public bool RequireAll;
    public bool PermissionsRejected;

    public ActionInteractableToy(SchematicBlockData block, InteractableToy toy, SchematicObject schematicObject)
    {
        Toy = toy;
        SchematicObject = schematicObject;
        BlockData = block;
        if (BlockData.Properties.TryGetValue("Permissions", out var permissions))
        {
            Permissions = (DoorPermissionFlags)Convert.ToUInt16(permissions);
        }
        if (BlockData.Properties.TryGetValue("RequireAll", out var requireAll))
        {
            RequireAll = Convert.ToBoolean(requireAll);
        }
    }

    public static void Register(SchematicBlockData block, InteractableToy toy, SchematicObject schematicObject)
    {
        ActionInteractableToy? actionInteractableToy = null;
        
        if (schematicObject.TryGetActionsByEventId(block.ObjectId, nameof(OnInteracted), out _) ||
            schematicObject.TryGetActionsByEventId(block.ObjectId, nameof(OnSearching), out _) ||
            schematicObject.TryGetActionsByEventId(block.ObjectId, nameof(OnSearched), out _) ||
            schematicObject.TryGetActionsByEventId(block.ObjectId, nameof(OnSearchAborted), out _))
        {
            actionInteractableToy = new(block, toy, schematicObject);
        }
        
        if (actionInteractableToy == null)
            return;
        
        Instances.Add(toy, actionInteractableToy);
    }

    public void OnInteracted(Player player)
    {
        SchematicObject.RunActionsByEventId(BlockData.ObjectId, nameof(OnInteracted), player);
    }

    public void OnSearchAborted(Player player)
    {
        SchematicObject.RunActionsByEventId(BlockData.ObjectId, nameof(OnSearchAborted), player);
    }

    public void OnSearching(Player player)
    {
        SchematicObject.RunActionsByEventId(BlockData.ObjectId, nameof(OnSearching), player);
    }

    public void OnSearched(Player player)
    {
        SchematicObject.RunActionsByEventId(BlockData.ObjectId, nameof(OnSearched), player);
    }

    public bool CheckPermissions(Player player)
    {
        var playerPermissions = DoorPermissionFlags.None;
        if (player.IsSCP)
        {
            playerPermissions = DoorPermissionFlags.ScpOverride;
        }

        if (player.CurrentItem is KeycardItem keycardItem)
        {
            playerPermissions = keycardItem.Permissions;
        }

        if (player.IsBypassEnabled)
            playerPermissions = DoorPermissionFlags.All;

        if (Permissions == DoorPermissionFlags.None)
            return true;

        return RequireAll ? playerPermissions.HasFlagAll(Permissions) : playerPermissions.HasFlagAny(Permissions);
    }
}