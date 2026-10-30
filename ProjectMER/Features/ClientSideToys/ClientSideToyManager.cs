using LabApi.Features.Wrappers;

namespace ProjectMER.Features.ClientSideToys;

public static class ClientSideToyManager
{
    private static readonly Dictionary<uint, ClientSideAdminToy> Toys = [];
    private static readonly Dictionary<Player, HashSet<uint>> SpawnedNetIds = [];

    public static void Register(ClientSideAdminToy toy)
    {
        if (Toys.Remove(toy.NetId, out ClientSideAdminToy? previous))
        {
            previous.DestroyForAll();
            Forget(toy.NetId);
        }

        Toys.Add(toy.NetId, toy);
        foreach (Player player in Player.ReadyList)
            Spawn(player, toy);
    }

    public static void SpawnFor(Player player)
    {
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;

        foreach (ClientSideAdminToy toy in Toys.Values)
            Spawn(player, toy);
    }

    public static void Unregister(uint netId)
    {
        if (Toys.Remove(netId, out ClientSideAdminToy? toy))
            toy.DestroyForAll();

        Forget(netId);
    }

    public static void RemovePlayer(Player player)
    {
        SpawnedNetIds.Remove(player);
    }

    public static void Clear()
    {
        foreach (ClientSideAdminToy toy in Toys.Values)
            toy.DestroyForAll();

        Toys.Clear();
        SpawnedNetIds.Clear();
    }

    private static void Spawn(Player player, ClientSideAdminToy toy)
    {
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;

        if (!SpawnedNetIds.TryGetValue(player, out HashSet<uint>? netIds))
        {
            netIds = [];
            SpawnedNetIds.Add(player, netIds);
        }

        if (netIds.Add(toy.NetId))
            toy.Spawn(player.ConnectionToClient);
    }

    private static void Forget(uint netId)
    {
        foreach (HashSet<uint> netIds in SpawnedNetIds.Values)
            netIds.Remove(netId);
    }
}
