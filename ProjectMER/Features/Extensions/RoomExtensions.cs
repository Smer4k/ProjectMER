using LabApi.Features.Wrappers;
using MapGeneration;
using NorthwoodLib.Pools;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Serializable;
using UnityEngine;

namespace ProjectMER.Features.Extensions;

public static class RoomExtensions
{
	public static Room GetRoomAtPosition(Vector3 position) => Room.TryGetRoomAtPosition(position, out Room? room) ? room : Room.List.First(x => x.Base != null && x.Name == RoomName.Outside);

    public static string GetRoomStringId(this Room room) => $"{room.Zone}_{room.Shape}_{room.GetExtendedRoomName()}";

	public static List<Room> GetRooms(this SerializableObject serializableObject)
	{
		string[] split = serializableObject.Room.Split('_');
		if (split.Length != 3)
			return ListPool<Room>.Shared.Rent(Room.List.Where(x => x.Base != null && x.Name == RoomName.Outside));

		FacilityZone facilityZone = (FacilityZone)Enum.Parse(typeof(FacilityZone), split[0], true);
		RoomShape roomShape = (RoomShape)Enum.Parse(typeof(RoomShape), split[1], true);
		ExtendedRoomName roomName = (ExtendedRoomName)Enum.Parse(typeof(ExtendedRoomName), split[2], true);

        return ListPool<Room>.Shared.Rent(Room.List.Where(x =>
            x.Base != null && x.Zone == facilityZone && x.Shape == roomShape && x.GetExtendedRoomName() == roomName));
    }

    public static int GetRoomIndex(this Room room)
    {
        ExtendedRoomName extendedName = room.GetExtendedRoomName();

        List<Room> list = ListPool<Room>.Shared.Rent(Room.List.Where(x =>
                x.Base != null 
                && x.Zone == room.Zone 
                && x.Shape == room.Shape 
                && x.GetExtendedRoomName() == extendedName));
        
        int index = list.IndexOf(room);
        ListPool<Room>.Shared.Return(list);
        return index;
    }

	public static Vector3 GetAbsolutePosition(this Room? room, Vector3 position)
	{
		if (room is null || room.Name == RoomName.Outside)
			return position;

		return room.Transform.TransformPoint(position);
	}

	public static Quaternion GetAbsoluteRotation(this Room? room, Vector3 eulerAngles)
	{
		if (room is null || room.Name == RoomName.Outside)
			return Quaternion.Euler(eulerAngles);

		return room.Transform.rotation * Quaternion.Euler(eulerAngles);
	}
	
	public static ExtendedRoomName GetExtendedRoomName(this Room room)
	{
		var name = (ExtendedRoomName)room.Name;
		if (name != ExtendedRoomName.Unnamed)
			return name;

		var str = room.GameObject.name.Remove(room.GameObject.name.Length - 7).Replace("_", string.Empty);
		Enum.TryParse(str, true, out name);
		return name;
	}
}
