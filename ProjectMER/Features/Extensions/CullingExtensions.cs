namespace ProjectMER.Features.Extensions;

using System.Reflection;
using LabApi.Features.Wrappers;
using Mirror;
using Objects;

/// <summary>
/// A set of useful extensions to easily interact with culling features.
/// </summary>
public static class CullingExtensions
{
	private static MethodInfo? _sendSpawnMessage;

	/// <summary>
	/// Gets a NetworkServer.SendSpawnMessage's <see cref="MethodInfo"/>.
	/// </summary>
	private static MethodInfo SendSpawnMessage => _sendSpawnMessage ??= typeof(NetworkServer).GetMethod("SendSpawnMessage", BindingFlags.NonPublic | BindingFlags.Static);

	/// <summary>
	/// Spawns the given <paramref name="schematic"/> for the specified <paramref name="player"/>.
	/// </summary>
	/// <param name="player">The target.</param>
	/// <param name="schematic">The schematic to spawn.</param>
	public static void SpawnSchematic(this Player player, SchematicObject schematic)
	{
		foreach (NetworkIdentity networkIdentity in schematic.NetworkIdentities)
			player.SpawnNetworkIdentity(networkIdentity);
	}

	/// <summary>
	/// Destroys the given <paramref name="schematic"/> for the specified <paramref name="player"/>.
	/// </summary>
	/// <param name="player">The target.</param>
	/// <param name="schematic">The schematic to destroy.</param>
	public static void DestroySchematic(this Player player, SchematicObject schematic)
	{
		foreach (NetworkIdentity networkIdentity in schematic.NetworkIdentities)
			player.DestroyNetworkIdentity(networkIdentity);
	}

	/// <summary>
	/// Spawns the given <paramref name="networkIdentity"/> for the specified <paramref name="player"/>.
	/// </summary>
	/// <param name="player">The target.</param>
	/// <param name="networkIdentity">The network identity to spawn.</param>
	public static void SpawnNetworkIdentity(this Player player, NetworkIdentity networkIdentity) =>
		SendSpawnMessage.Invoke(null, [networkIdentity, player.Connection]);

	/// <summary>
	/// Destroys the given <paramref name="networkIdentity"/> for the specified <paramref name="player"/>.
	/// </summary>
	/// <param name="player">The target.</param>
	/// <param name="networkIdentity">The network identity to destroy.</param>
	public static void DestroyNetworkIdentity(this Player player, NetworkIdentity networkIdentity) =>
		player.Connection.Send(new ObjectDestroyMessage { netId = networkIdentity.netId });
}