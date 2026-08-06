using AdminToys;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using RelativePositioning;
using UnityEngine;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using WaypointToy = AdminToys.WaypointToy;

namespace ProjectMER.Features.Objects;

public sealed class CullingZoneObject : MonoBehaviour
{
    public static readonly List<CullingZoneObject> AllCullingZone = [];
    public readonly List<CullingZoneObject> ConnectedZones = [];
    public int NumberOfObjectPerSpawn;
    public float ExitDebounceSeconds = 0.5f;
    public int BlocksCount => _networkIdentities.Count;
    
    private readonly Dictionary<uint, int> _insidePlayers = new();
    private readonly Dictionary<uint, CancellationTokenSource> _pendingHides = new();
    private readonly Dictionary<Player, int> _awaitingSpawn = new();
    private readonly HashSet<uint> _loadedPlayers = [];

    private readonly List<Player> _awaitingSpawnSnapshotBuffer = [];
    private readonly List<NetworkIdentity> _networkIdentities = [];

    private bool _processingAwaiting;

    public bool Contains(Player player) => _insidePlayers.ContainsKey(player.NetworkId);

    private void Start()
    {
        AllCullingZone.Add(this);
    }

    private void OnDestroy()
    {
        AllCullingZone.Remove(this);
        foreach (var networkIdentity in _networkIdentities)
        {
            networkIdentity.ClearObservers();
        }

        Timing.CallDelayed(0.3f, () =>
        {
            foreach (var player in Player.ReadyList)
            {
                if (player.IsDestroyed || player.IsDummy || player.IsNpc)
                    continue;
                foreach (var observer in player.ConnectionToClient.observing.ToList())
                {
                    if (observer == null)
                        player.ConnectionToClient.observing.Remove(observer);
                }
            }
        });
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        AddPlayer(player);
        foreach (var zone in ConnectedZones)
        {
            zone.AddPlayer(player);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        RemovePlayer(player);
        
        foreach (var zone in ConnectedZones)
        {
            zone.RemovePlayer(player);
        }
    }

    public async Awaitable InitializeAsync()
    {
        _networkIdentities.Clear();

        var queue = new Queue<Transform>();
        foreach (Transform child in transform)
            queue.Enqueue(child);

        float budgetDeadline = Time.realtimeSinceStartup + 2f / 1000f;

        while (queue.Count > 0)
        {
            if (Time.realtimeSinceStartup >= budgetDeadline)
            {
                await Awaitable.NextFrameAsync();
                budgetDeadline = Time.realtimeSinceStartup + 2f / 1000f;
            }

            var current = queue.Dequeue();
            if (current == null || current.TryGetComponent<CullingZoneObject>(out _))
                continue;
            if (current.TryGetComponent(out PrimitiveObjectToy primitiveObjectToy) &&
                primitiveObjectToy.gameObject.name == Scp106PassableObject.ColliderName)
                continue;

            if (!current.TryGetComponent<WaypointBase>(out _) &&
                !current.TryGetComponent<Scp079CameraToy>(out _) &&
                !current.TryGetComponent<PlayerBlockerObject>(out _) &&
                current.TryGetComponent<NetworkIdentity>(out var networkIdentity))
            {
                networkIdentity.visible = Visibility.ForceHidden;
                foreach (var connectionToClient in networkIdentity.observers.Values)
                    connectionToClient?.RemoveFromObserving(networkIdentity, false);
                networkIdentity.observers.Clear();
                _networkIdentities.Add(networkIdentity);
            }

            foreach (Transform child in current)
                queue.Enqueue(child);
        }
    }

    private async Awaitable ProcessAwaitingAsync()
    {
        if (_networkIdentities.Count == 0)
            return;
        _processingAwaiting = true;
        try
        {
            while (_awaitingSpawn.Count > 0)
            {
                if (destroyCancellationToken.IsCancellationRequested)
                    return;

                _awaitingSpawnSnapshotBuffer.Clear();
                _awaitingSpawnSnapshotBuffer.AddRange(_awaitingSpawn.Keys);

                foreach (var player in _awaitingSpawnSnapshotBuffer)
                {
                    if (player.IsDestroyed)
                    {
                        _awaitingSpawn.Remove(player);
                        continue;
                    }

                    if (!_insidePlayers.ContainsKey(player.NetworkId))
                    {
                        continue;
                    }

                    var spectators = player.CurrentSpectators.ToList();

                    var index = _awaitingSpawn[player];
                    var end = Mathf.Min(index + NumberOfObjectPerSpawn, _networkIdentities.Count);
                    for (var i = index; i < end; i++)
                    {
                        if (destroyCancellationToken.IsCancellationRequested)
                            return;
                        player.ConnectionToClient.AddToObserving(_networkIdentities[i]);
                        foreach (var spectator in spectators)
                        {
                            spectator.ConnectionToClient.AddToObserving(_networkIdentities[i]);
                        }
                    }

                    if (end >= _networkIdentities.Count)
                    {
                        _loadedPlayers.Add(player.NetworkId);
                        _awaitingSpawn.Remove(player);
                    }
                    else
                    {
                        _awaitingSpawn[player] = end;
                    }
                }

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _processingAwaiting = false;
        }
    }

    public void AddPlayer(Player player)
    {
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;

        var hadPendingHide = _pendingHides.Remove(player.NetworkId, out var pendingCts);
        pendingCts?.Cancel();

        var count = _insidePlayers.GetValueOrDefault(player.NetworkId) + 1;
        _insidePlayers[player.NetworkId] = count;
        if (BlocksCount == 0)
            return;
        
        if (count > 1 || (_loadedPlayers.Contains(player.NetworkId) && hadPendingHide))
            return;

        if (NumberOfObjectPerSpawn > 0)
        {
            _awaitingSpawn.TryAdd(player, 0);

            if (!_processingAwaiting)
                _ = ProcessAwaitingAsync();
            return;
        }

        ShowFor(player);
        _loadedPlayers.Add(player.NetworkId);
    }

    public void RemovePlayer(Player player)
    {
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;

        if (!_insidePlayers.TryGetValue(player.NetworkId, out var count))
            return;

        if (count > 1)
        {
            _insidePlayers[player.NetworkId] = count - 1;
            return;
        }

        _insidePlayers.Remove(player.NetworkId);
        
        if (BlocksCount == 0)
            return;

        if (_pendingHides.Remove(player.NetworkId, out var oldCts))
            oldCts?.Cancel();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        _pendingHides[player.NetworkId] = cts;
        _ = DebouncedHideAsync(player, cts);
    }

    private async Awaitable DebouncedHideAsync(Player player, CancellationTokenSource cts)
    {
        try
        {
            await Awaitable.WaitForSecondsAsync(ExitDebounceSeconds, cts.Token);

            if (_insidePlayers.ContainsKey(player.NetworkId))
                return;

            _loadedPlayers.Remove(player.NetworkId);

            HideFor(player);
            _awaitingSpawn.Remove(player);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (_pendingHides.TryGetValue(player.NetworkId, out var current) && current == cts)
                _pendingHides.Remove(player.NetworkId);

            cts.Dispose();
        }
    }

    public void ShowFor(Player player)
    {
        if (_networkIdentities.Count == 0)
            return;
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;
        var spectators = player.CurrentSpectators;
        foreach (var identity in _networkIdentities)
        {
            player.ConnectionToClient.AddToObserving(identity);
            foreach (var spectator in spectators)
            {
                spectator.ConnectionToClient.AddToObserving(identity);
            }
        }
    }

    public void HideFor(Player player)
    {
        if (_networkIdentities.Count == 0)
            return;
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;
        var spectators = player.CurrentSpectators;
        
        if (!_awaitingSpawn.TryGetValue(player, out var index))
        {
            index = _networkIdentities.Count;
        }
        index = Mathf.Clamp(index, 0, _networkIdentities.Count);

        for (var i = 0; i < index; i++)
        {
            player.ConnectionToClient.RemoveFromObserving(_networkIdentities[i], false);
            foreach (var spectator in spectators)
            {
                spectator.ConnectionToClient.RemoveFromObserving(_networkIdentities[i], false);
            }
        }
    }
}