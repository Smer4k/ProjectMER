using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using NorthwoodLib.Pools;
using ProjectMER.Features.ClientSideToys;
using ProjectMER.Features.Enums;
using RelativePositioning;
using UnityEngine;
using LightSourceToy = AdminToys.LightSourceToy;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using SpeakerToy = AdminToys.SpeakerToy;
using TextToy = AdminToys.TextToy;

namespace ProjectMER.Features.Objects;

public sealed class CullingZoneObject : MonoBehaviour
{
    public static readonly List<CullingZoneObject> AllCullingZone = [];
    public readonly List<CullingZoneObject> ConnectedZones = [];
    public int NumberOfObjectPerSpawn;
    public float ExitDebounceSeconds = 0.5f;
    public int BlocksCount => _networkIdentities.Count;
    public bool Pause = true;

    private readonly Dictionary<uint, HashSet<CullingZoneObject>> _insidePlayers = new();
    private readonly Dictionary<uint, CancellationTokenSource> _pendingHides = new();
    private readonly Dictionary<Player, int> _awaitingSpawn = new();
    private readonly HashSet<uint> _loadedPlayers = [];

    private readonly List<Player> _awaitingSpawnSnapshotBuffer = [];
    private readonly List<NetworkIdentity> _networkIdentities = [];
    private readonly HashSet<uint> _netIds = [];
    private readonly Dictionary<uint, ClientSideAdminToy> _clientSideAdminToys = new();
    private bool _processingAwaiting;

    #region State

    public bool Contains(Player player) => _insidePlayers.ContainsKey(player.NetworkId);

    public bool Contains(NetworkIdentity networkIdentity)
    {
        return _netIds.Contains(networkIdentity.netId);
    }

    public void OnPlayerLeft(Player player)
    {
        _awaitingSpawn.Remove(player);
        if (_pendingHides.TryGetValue(player.NetworkId, out var cts))
            cts.Cancel();
        _insidePlayers.Remove(player.NetworkId);
        _loadedPlayers.Remove(player.NetworkId);
    }
    
    public void Init()
    {
        if (_networkIdentities.Count == 0)
            return;
        foreach (var networkIdentity in _networkIdentities)
        {
            networkIdentity.visible = Visibility.ForceHidden;
            NetworkServer.SendToObservers<ObjectHideMessage>(networkIdentity, new ObjectHideMessage()
            {
                netId = networkIdentity.netId
            });
            networkIdentity.ClearObservers();
        }

        Pause = false;
    }

    #endregion

    #region Unity lifecycle

    private void Start()
    {
        AllCullingZone.Add(this);
    }

    private void OnDestroy()
    {
        _processingAwaiting = false;
        AllCullingZone.Remove(this);
    }

    #endregion

    #region Trigger handling

    private void OnTriggerEnter(Collider other)
    {
        if (Pause)
            return;

        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        UpdatePlayerPresence(player, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (Pause)
            return;

        if (!other.CompareTag("Player"))
            return;

        Player? player = Player.Get(other.gameObject);
        if (player is null)
            return;

        UpdatePlayerPresence(player, false);
    }

    #endregion

    #region Spawn queue

    private async Awaitable ProcessAwaitingAsync()
    {
        if (Pause || _networkIdentities.Count == 0)
            return;
        _processingAwaiting = true;
        try
        {
            while (_awaitingSpawn.Count > 0)
            {
                if (Pause || destroyCancellationToken.IsCancellationRequested)
                    return;

                _awaitingSpawnSnapshotBuffer.Clear();
                _awaitingSpawnSnapshotBuffer.AddRange(_awaitingSpawn.Keys);
                var needRefreshNetIds = false;

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

                    var spectators = player.CurrentSpectators;
                    try
                    {
                        var index = _awaitingSpawn[player];
                        var end = Mathf.Min(index + NumberOfObjectPerSpawn, _networkIdentities.Count);
                        for (var i = index; i < end; i++)
                        {
                            if (_networkIdentities[i] == null)
                            {
                                _networkIdentities.RemoveAt(i);
                                i--;
                                end = Mathf.Min(index + NumberOfObjectPerSpawn, _networkIdentities.Count);
                                needRefreshNetIds = true;
                                continue;
                            }

                            if (_clientSideAdminToys.TryGetValue(_networkIdentities[i].netId,
                                    out var clientSideAdminToy))
                            {
                                clientSideAdminToy.Spawn(player.ConnectionToClient);
                                foreach (var spectator in spectators)
                                {
                                    if (spectator == null || spectator.IsDestroyed || spectator.IsDummy ||
                                        spectator.IsNpc)
                                        continue;
                                    clientSideAdminToy.Spawn(spectator.ConnectionToClient);
                                }

                                continue;
                            }

                            _networkIdentities[i].AddObserver(player.ConnectionToClient);
                            foreach (var spectator in spectators)
                            {
                                if (spectator == null || spectator.IsDestroyed || spectator.IsDummy || spectator.IsNpc)
                                    continue;

                                _networkIdentities[i].AddObserver(spectator.ConnectionToClient);
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
                    finally
                    {
                        ListPool<Player>.Shared.Return(spectators);
                    }
                }

                if (needRefreshNetIds)
                    RefreshNetIds();

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Warn($"Operation canceled for CullingZone ({gameObject.name})");
            return;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString());
        }
        finally
        {
            _processingAwaiting = false;
        }
    }

    #endregion

    #region Object collection

    public void RegisterObject(GameObject go, BlockType blockType)
    {
        if (!go.TryGetComponent<NetworkBehaviour>(out _) ||
            !go.TryGetComponent<NetworkIdentity>(out var networkIdentity))
        {
            return;
        }

        if (blockType is BlockType.AudioPlayer or BlockType.Camera or BlockType.Waypoint or BlockType.Door
                or BlockType.PlayerBlocker or BlockType.CameraTransfer || go.TryGetComponent<Scp106PassableObject>(out _))
        {
            RemoveParentObjects(go.transform);
            return;
        }

        if (go.transform.parent.TryGetComponent<Scp106PassableObject>(out var childObject) && childObject.Collider.gameObject == go)
        {
            RemoveParentObjects(childObject.transform);
            return;
        }

        _networkIdentities.Add(networkIdentity);
        _netIds.Add(networkIdentity.netId);

        if (go.GetComponentInParent<AnimatorMarker>() == null && go.TryGetComponent<AdminToyBase>(out var adminToyBase) && adminToyBase.NetworkIsStatic)
        {
            ClientSideAdminToy? clientSideAdminToy = null;
            if (adminToyBase is PrimitiveObjectToy primitiveObjectToy)
            {
                clientSideAdminToy = new ClientSidePrimitive(primitiveObjectToy);
            }
            else if (adminToyBase is LightSourceToy lightSourceToy)
            {
                clientSideAdminToy = new ClientSideLightSourceToy(lightSourceToy);
            }
            else if (adminToyBase is TextToy textToy)
            {
                clientSideAdminToy = new ClientSideTextToy(textToy);
            }

            if (clientSideAdminToy != null)
            {
                _clientSideAdminToys[networkIdentity.netId] = clientSideAdminToy;
            }
        }
    }

    public async Awaitable InitializeAsync()
    {
        _networkIdentities.Clear();

        var queue = new Queue<Transform>();
        foreach (Transform child in transform)
            queue.Enqueue(child);

        float budgetDeadline = Time.realtimeSinceStartup + 10f / 1000f;

        while (queue.Count > 0)
        {
            if (Time.realtimeSinceStartup >= budgetDeadline)
            {
                await Awaitable.NextFrameAsync();
                budgetDeadline = Time.realtimeSinceStartup + 10f / 1000f;
            }

            var current = queue.Dequeue();
            if (current == null || current.TryGetComponent<CullingZoneObject>(out _))
                continue;
            if (current.TryGetComponent(out PrimitiveObjectToy primitiveObjectToy) &&
                primitiveObjectToy.gameObject.name == Scp106PassableObject.ColliderName)
                continue;

            if (current.TryGetComponent<NetworkIdentity>(out var networkIdentity))
            {
                _netIds.Add(networkIdentity.netId);
                _networkIdentities.Add(networkIdentity);
            }

            if (current.TryGetComponent<WaypointBase>(out _) ||
                current.TryGetComponent<Scp079CameraToy>(out _) ||
                current.TryGetComponent<PlayerBlockerObject>(out _) ||
                current.TryGetComponent<Scp106PassableObject>(out _) ||
                current.TryGetComponent<SpeakerToy>(out _))
            {
                RemoveParentObjects(current);
            }
            else
            {
                ClientSideAdminToy? clientSideAdminToy = null;
                if (current.TryGetComponent(out primitiveObjectToy))
                    clientSideAdminToy = new ClientSidePrimitive(primitiveObjectToy);
                else if (current.TryGetComponent(out LightSourceToy lightSourceToy))
                    clientSideAdminToy = new ClientSideLightSourceToy(lightSourceToy);
                else if (current.TryGetComponent(out TextToy textToy))
                    clientSideAdminToy = new ClientSideTextToy(textToy);

                if (clientSideAdminToy != null)
                {
                    _clientSideAdminToys[networkIdentity.netId] = clientSideAdminToy;
                }

                networkIdentity.visible = Visibility.ForceHidden;
                NetworkServer.SendToObservers<ObjectHideMessage>(networkIdentity, new ObjectHideMessage()
                {
                    netId = networkIdentity.netId
                });
                networkIdentity.ClearObservers();
            }

            foreach (Transform child in current)
                queue.Enqueue(child);
        }
    }

    public void RefreshNetIds()
    {
        _netIds.Clear();
        foreach (var networkIdentity in _networkIdentities)
        {
            if (networkIdentity == null)
                continue;
            _netIds.Add(networkIdentity.netId);
        }
    }

    private void RemoveParentObjects(Transform current)
    {
        if (current == null)
            return;

        while (true)
        {
            if (current.TryGetComponent<CullingZoneObject>(out _) || current.TryGetComponent<SchematicObject>(out _))
                return;
            if (!current.TryGetComponent<NetworkIdentity>(out var networkIdentity))
            {
                if (current.parent == null)
                    return;
                current = current.parent;
                continue;
            }

            _netIds.Remove(networkIdentity.netId);
            _networkIdentities.Remove(networkIdentity);
            if (current.parent == null)
                return;
            current = current.parent;
        }
    }

    public void RemoveObject(NetworkIdentity target)
    {
        if (target == null)
            return;

        RemoveSingleObject(target);
        var netIds = target.GetComponentsInChildren<NetworkIdentity>();
        foreach (var identity in netIds)
        {
            RemoveSingleObject(identity);
        }
    }

    private void RemoveSingleObject(NetworkIdentity target)
    {
        if (!Contains(target))
            return;

        var removedIndex = _networkIdentities.IndexOf(target);
        if (removedIndex < 0)
            return;

        _netIds.Remove(target.netId);
        _networkIdentities.RemoveAt(removedIndex);
        target.visible = Visibility.Default;
        target.transform.SetParent(null);

        if (_awaitingSpawn.Count <= 0)
            return;

        foreach (var player in _awaitingSpawn.Keys.ToList())
        {
            if (_awaitingSpawn[player] > removedIndex)
                _awaitingSpawn[player]--;
        }
    }

    #endregion

    #region Player presence

    private void UpdatePlayerPresence(Player player, bool isEntering)
    {
        UpdatePlayerPresenceForZone(player, this, isEntering);
        foreach (var zone in ConnectedZones)
        {
            zone.UpdatePlayerPresenceForZone(player, this, isEntering);
        }

        ForEachSpectator(player, spectator =>
        {
            UpdatePlayerPresenceForZone(spectator, this, isEntering);
            foreach (var zone in ConnectedZones)
            {
                zone.UpdatePlayerPresenceForZone(spectator, this, isEntering);
            }
        });
    }

    private void UpdatePlayerPresenceForZone(Player player, CullingZoneObject source, bool isEntering)
    {
        if (isEntering)
            AddPlayer(player, source);
        else
            RemovePlayer(player, source);
    }

    public void AddPlayer(Player player) => AddPlayer(player, this);

    private void AddPlayer(Player player, CullingZoneObject source)
    {
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;

        var hadPendingHide = _pendingHides.Remove(player.NetworkId, out var pendingCts);
        pendingCts?.Cancel();

        if (!_insidePlayers.TryGetValue(player.NetworkId, out var sources))
        {
            sources = [];
            _insidePlayers[player.NetworkId] = sources;
        }

        var wasInside = sources.Count > 0;
        sources.Add(source);
        if (BlocksCount == 0)
            return;

        if (wasInside || (_loadedPlayers.Contains(player.NetworkId) && hadPendingHide))
            return;

        if (NumberOfObjectPerSpawn > 0)
        {
            _awaitingSpawn.TryAdd(player, 0);

            if (!_processingAwaiting && !Pause)
            {
                _ = ProcessAwaitingAsync();
            }

            return;
        }

        if (!Pause)
            ShowFor(player);

        _loadedPlayers.Add(player.NetworkId);
    }

    public void RemovePlayer(Player player) => RemovePlayer(player, null);

    private void RemovePlayer(Player player, CullingZoneObject? source)
    {
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;

        if (!_insidePlayers.TryGetValue(player.NetworkId, out var sources))
            return;

        if (source == null)
            sources.Clear();
        else
            sources.Remove(source);

        if (sources.Count > 0)
            return;

        _insidePlayers.Remove(player.NetworkId);

        if (BlocksCount == 0)
            return;

        if (_pendingHides.Remove(player.NetworkId, out var oldCts))
            oldCts?.Cancel();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        _pendingHides[player.NetworkId] = cts;
        _ = DebouncedHideAsync(player, cts);
    }

    #endregion

    #region Delayed hide

    private async Awaitable DebouncedHideAsync(Player player, CancellationTokenSource cts)
    {
        try
        {
            await Awaitable.WaitForSecondsAsync(ExitDebounceSeconds, cts.Token);

            if (_insidePlayers.ContainsKey(player.NetworkId))
                return;

            _loadedPlayers.Remove(player.NetworkId);

            if (!Pause)
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

    #endregion

    #region Network visibility

    public void ShowFor(Player player)
    {
        if (_networkIdentities.Count == 0)
            return;
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;

        foreach (var identity in _networkIdentities)
            identity.AddObserver(player.ConnectionToClient);

        ForEachSpectator(player, spectator =>
        {
            foreach (var identity in _networkIdentities)
                identity.AddObserver(spectator.ConnectionToClient);
        });
    }

    public void HideFor(Player player)
    {
        if (_networkIdentities.Count == 0)
            return;
        if (player == null || player.IsDestroyed || player.IsDummy || player.IsNpc)
            return;
        if (!_awaitingSpawn.TryGetValue(player, out var index))
        {
            index = _networkIdentities.Count;
        }

        index = Mathf.Clamp(index, 0, _networkIdentities.Count);

        for (var i = 0; i < index; i++)
        {
            player.ConnectionToClient.RemoveFromObserving(_networkIdentities[i], false);
            _networkIdentities[i].RemoveObserver(player.ConnectionToClient);
        }

        ForEachSpectator(player, spectator =>
        {
            for (var i = 0; i < index; i++)
            {
                spectator.ConnectionToClient.RemoveFromObserving(_networkIdentities[i], false);
                _networkIdentities[i].RemoveObserver(spectator.ConnectionToClient);
            }
        });
    }

    private static void ForEachSpectator(Player player, Action<Player> action)
    {
        var spectators = player.CurrentSpectators;
        try
        {
            foreach (var spectator in spectators)
            {
                if (spectator == null || spectator.IsDestroyed || spectator.IsDummy || spectator.IsNpc)
                    continue;

                action(spectator);
            }
        }
        finally
        {
            ListPool<Player>.Shared.Return(spectators);
        }
    }

    #endregion
}
