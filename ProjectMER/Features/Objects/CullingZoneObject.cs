using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using ProjectMER.Features.Interfaces;
using RelativePositioning;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public sealed class CullingZoneObject : MonoBehaviour, ICullingContainer
{
    public static readonly List<CullingZoneObject> AllCullingZone = [];
    public int NumberOfObjectPerSpawn;
    public float ExitDebounceSeconds = 0.5f;

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
        foreach (var networkId in _insidePlayers.Keys)
        {
            var player = Player.Get(networkId);
            if (player == null)
                continue;
            foreach (var networkIdentity in _networkIdentities)
            {
                player.ConnectionToClient.RemoveFromObserving(networkIdentity, true);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        AddPlayer(player);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        RemovePlayer(player);
    }

    public async Awaitable InitializeAsync()
    {
        _networkIdentities.Clear();

        var stack = new Stack<Transform>();
        foreach (Transform child in transform)
            stack.Push(child);

        float budgetDeadline = Time.realtimeSinceStartup + 2f / 1000f;

        while (stack.Count > 0)
        {
            if (Time.realtimeSinceStartup >= budgetDeadline)
            {
                await Awaitable.NextFrameAsync();
                budgetDeadline = Time.realtimeSinceStartup + 2f / 1000f;
            }

            var current = stack.Pop();
            if (current == null || current.TryGetComponent<CullingZoneObject>(out _))
                continue;

            if (!current.TryGetComponent<WaypointBase>(out _) &&
                !current.TryGetComponent<Scp079CameraToy>(out _) &&
                current.TryGetComponent<NetworkIdentity>(out var networkIdentity))
            {
                networkIdentity.visible = Visibility.ForceHidden;
                foreach (var connectionToClient in networkIdentity.observers.Values)
                    connectionToClient.RemoveFromObserving(networkIdentity, false);
                networkIdentity.observers.Clear();
                _networkIdentities.Add(networkIdentity);
            }

            foreach (Transform child in current)
                stack.Push(child);
        }
    }

    private async Awaitable ProcessAwaitingAsync()
    {
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
            _awaitingSpawn.Remove(player);

            HideFor(player);
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
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;
        var spectators = player.CurrentSpectators;
        foreach (var identity in _networkIdentities)
        {
            player.ConnectionToClient.RemoveFromObserving(identity, false);
            foreach (var spectator in spectators)
            {
                spectator.ConnectionToClient.RemoveFromObserving(identity, false);
            }
        }
    }
}