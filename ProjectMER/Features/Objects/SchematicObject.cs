using AdminToys;
using InventorySystem.Items.Pickups;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using ProjectMER.Events.Handlers;
using ProjectMER.Features.Actions;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Serializable;
using ProjectMER.Features.Serializable.Schematics;
using UnityEngine;
using Utf8Json;
using Utils.NonAllocLINQ;
using Locker = MapGeneration.Distributors.Locker;
using Object = UnityEngine.Object;

namespace ProjectMER.Features.Objects;

public class SchematicObject : MonoBehaviour
{
	/// <summary>
	/// Gets the schematic name.
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// Gets a schematic directory path.
	/// </summary>
	public string DirectoryPath { get; private set; }

	/// <summary>
	/// Gets or sets the global position of the object.
	/// </summary>
	public Vector3 Position
	{
		get => transform.position;
		set
		{
			transform.position = value;
		}
	}

	/// <summary>
	/// Gets or sets the global rotation of the object.
	/// </summary>
	public Quaternion Rotation
	{
		get => transform.rotation;
		set
		{
			transform.rotation = value;
		}
	}

	/// <summary>
	/// Gets or sets the global euler angles of the object.
	/// </summary>
	public Vector3 EulerAngles
	{
		get => Rotation.eulerAngles;
		set => Rotation = Quaternion.Euler(value);
	}

	/// <summary>
	/// Gets or sets the scale of the object.
	/// </summary>
	public Vector3 Scale
	{
		get => transform.localScale;
		set
		{
			transform.localScale = value;
		}
	}

	public IReadOnlyList<GameObject> AttachedBlocks
	{
		get
		{
			if (_attachedBlocks.Count != 0 && _attachedBlocks.All(x => x != null))
				return _attachedBlocks;

			_attachedBlocks.Clear();
			foreach (Transform transform in GetComponentsInChildren<Transform>())
			{
				if (transform == this.transform)
					continue;

				_attachedBlocks.Add(transform.gameObject);
			}

			return _attachedBlocks;
		}
	}

	public IReadOnlyList<NetworkIdentity> NetworkIdentities
	{
		get
		{
			if (_networkIdentities.Count > 0 && _networkIdentities.All(x => x != null))
				return _networkIdentities;

			_networkIdentities.Clear();
			foreach (GameObject block in AttachedBlocks)
			{
				if (block.TryGetComponent(out NetworkIdentity networkIdentity))
					_networkIdentities.Add(networkIdentity);
			}

			return _networkIdentities;
		}
	}

	public IReadOnlyList<AdminToyBase> AdminToyBases
	{
		get
		{
			if (_adminToyBases.Count > 0 && _adminToyBases.All(x => x != null))
				return _adminToyBases;

			_adminToyBases.Clear();
			foreach (NetworkIdentity netId in NetworkIdentities)
			{
				if (netId.TryGetComponent(out AdminToyBase adminToyBase))
					_adminToyBases.Add(adminToyBase);
			}

			return _adminToyBases;
		}
	}

	public AnimationController AnimationController => AnimationController.Get(this);

	public SchematicObject Init(SchematicObjectDataList data)
	{
		Name = Path.GetFileNameWithoutExtension(data.Path);
		DirectoryPath = data.Path;

		ObjectFromId = new Dictionary<int, Transform>(data.Blocks.Count + 1)
		{
			{ data.RootObjectId, transform },
		};

		ActionHostsByObjectId.Clear();
		ActionsByObjectId.Clear();

		CreateRecursiveFromID(data.RootObjectId, data.Blocks, transform);
		AddRigidbodies();
		AddAnimators();
		InitCullingZones(data.Blocks);
		
		Timing.CallDelayed(0.3f, () =>
		{
			foreach (var playerBlockers in transform.GetComponentsInChildren<PlayerBlockerObject>())
			{
				playerBlockers.UpdateVisibility();
			}

			foreach (var cullingZone in transform.GetComponentsInChildren<CullingZoneObject>())
			{
				_ = cullingZone.InitializeAsync();
			}
		});

		Timing.CallDelayed(2f, () =>
		{
			foreach (var locker in transform.GetComponentsInChildren<Locker>())
			{
				foreach (var itemPickupBase in locker.GetComponentsInChildren<ItemPickupBase>())
				{
					if (itemPickupBase.TryGetComponent(out Rigidbody rigidbody))
						rigidbody.isKinematic = false;
				}
			}
		});
		
		// coroutine
		foreach (var damageableObject in transform.GetComponentsInChildren<DamageableObject>())
		{
			damageableObject.RegisterChildDestructibles(data.Blocks);
		}
		
		Schematic.OnSchematicSpawned(new(this, Name));

		return this;
	}

	private void CreateRecursiveFromID(int id, List<SchematicBlockData> blocks, Transform parentGameObject)
	{
		SchematicBlockData? blockData = blocks.Find(c => c.ObjectId == id);
		Transform? childGameObjectTransform = transform; // Create the object first before creating children.

		if (blockData != null)
			childGameObjectTransform = CreateObject(blockData, parentGameObject);
		
		if (childGameObjectTransform == null)
			return;
		
		int[] parentSchematics = blocks.Where(bl => bl.BlockType == BlockType.Schematic).Select(bl => bl.ObjectId).ToArray();

		// Gets all the ObjectIds of all the schematic blocks inside "blocks" argument.
		foreach (SchematicBlockData block in blocks.FindAll(c => c.ParentId == id))
		{
			if (parentSchematics.Contains(block.ParentId)) // The block is a child of some schematic inside "parentSchematics" array, therefore it will be skipped to avoid spawning it and its children twice.
				continue;

			CreateRecursiveFromID(block.ObjectId, blocks, childGameObjectTransform); // The child now becomes the parent
		}
	}

	private Transform? CreateObject(SchematicBlockData block, Transform parentTransform)
	{
		if (block == null)
			return null;

		GameObject? gameObject = block.Create(this, parentTransform);
		
		if (gameObject == null)
			return null;
		
		if (block.BlockType == BlockType.Camera)
			gameObject.GetComponent<Scp079CameraToy>()?.SetRoom(null, null);

		if (block.BlockType != BlockType.Teleport && block.BlockType != BlockType.PlayerSpawnPoint)
			NetworkServer.Spawn(gameObject);
		
		ObjectFromId.Add(block.ObjectId, gameObject.transform);

		if (ProjectMER.Singleton.Config.ActionEnabled)
		{
			RegisterActionHost(block);

			if (block.BlockType == BlockType.Interactable)
			{
				ActionInteractableToy.Register(block, InteractableToy.Get(gameObject.GetComponent<InvisibleInteractableToy>()), this);
			}
		}
		
		if (block.BlockType != BlockType.Light && TryGetAnimatorController(block.AnimatorName, out RuntimeAnimatorController animatorController))
			_animators.Add(gameObject, animatorController);

		return gameObject.transform;
	}

	private void RegisterActionHost(SchematicBlockData block)
	{
		List<ActionEventList> actionEvents = ActionEventSerialization.ReadEventListsFromProperties(block.Properties);
		if (actionEvents.Count == 0)
			return;
			
		ActionEventHostObject actionHost = new(this, block.ObjectId);
		actionHost.SetActionEvents(actionEvents);

		ActionHostsByObjectId[block.ObjectId] = actionHost;
		ActionsByObjectId[block.ObjectId] = actionHost.ActionsByEventId;
	}

	public bool TryGetActionsByEventId(int objectId, string eventId, out List<ActionGame> actions)
	{
		actions = null!;

		if (!ActionsByObjectId.TryGetValue(objectId, out Dictionary<string, List<ActionGame>> actionsByEventId))
			return false;

		return actionsByEventId.TryGetValue(eventId, out actions);
	}

	public CoroutineHandle RunActionsByEventId(int objectId, string eventId, Player? target = null)
	{
		if (!ActionHostsByObjectId.TryGetValue(objectId, out ActionEventHostObject actionHost))
			return default;

		return actionHost.RunActions(eventId, target);
	}
	
	private bool TryGetAnimatorController(string animatorName, out RuntimeAnimatorController animatorController)
	{
		animatorController = null!;

		if (string.IsNullOrEmpty(animatorName))
			return false;

		Object? animatorObject = null;
		var list = AssetBundle.GetAllLoadedAssetBundles();
		if (list != null)
		{
			AssetBundle? assetBundle = null;
			foreach (var asset in list)
			{
				if (asset?.mainAsset?.name == animatorName)
				{
					assetBundle = asset;
					break;
				}
			}

			if (assetBundle != null)
			{
				foreach (var asset in assetBundle.LoadAllAssets())
				{
					if (asset is RuntimeAnimatorController controller)
					{
						animatorObject = controller;
						break;
					}
				}
			}
		}
		
		if (animatorObject is null)
		{
			string path = Path.Combine(DirectoryPath, animatorName);

			if (!File.Exists(path))
			{
				Logger.Warn($"{gameObject.name} block of schematic should have a {animatorName} animator attached, but the file does not exist!");
				return false;
			}

			var assets = AssetBundle.LoadFromFile(path).LoadAllAssets();
			foreach (var asset in assets)
			{
				if (asset is RuntimeAnimatorController controller)
				{
					animatorObject = controller;
					break;
				}
			}
		}
		
		if (animatorObject == null)
		{
			Logger.Error($"Animator {animatorName} not found!");
			return false;
		}
		
		animatorController = (RuntimeAnimatorController)animatorObject;
		return true;
	}

	private bool AddAnimators()
	{
		bool isAnimated = false;
		if (!_animators.IsEmpty())
		{
			isAnimated = true;
			foreach (KeyValuePair<GameObject, RuntimeAnimatorController> pair in _animators)
				pair.Key.AddComponent<Animator>().runtimeAnimatorController = pair.Value;
		}

		_animators.Clear();
		AssetBundle.UnloadAllAssetBundles(false);
		return isAnimated;
	}

	private bool AddRigidbodies()
	{
		bool hasRigidbodies = false;
		string rigidbodyPath = Path.Combine(DirectoryPath, $"{Name}-Rigidbodies.json");
		if (!File.Exists(rigidbodyPath))
			return false;

		foreach (KeyValuePair<int, SerializableRigidbody> dict in JsonSerializer.Deserialize<Dictionary<int, SerializableRigidbody>>(File.ReadAllText(rigidbodyPath)))
		{
			if (!ObjectFromId.TryGetValue(dict.Key, out Transform transform))
				continue;

			if (!transform.gameObject.TryGetComponent(out Rigidbody rigidbody))
				rigidbody = transform.gameObject.AddComponent<Rigidbody>();

			rigidbody.isKinematic = dict.Value.IsKinematic;
			rigidbody.useGravity = dict.Value.UseGravity;
			rigidbody.constraints = dict.Value.Constraints;
			rigidbody.mass = dict.Value.Mass;

			hasRigidbodies = true;
		}

		return hasRigidbodies;
	}

	private void InitCullingZones(List<SchematicBlockData> blocks)
	{
		foreach (var block in blocks)
		{
			if (block.BlockType != BlockType.CullingZone)
				continue;
			if (!block.Properties.TryGetValue("ConnectedZones", out var connectedZonesObj))
			{
				continue;
			}
			var connector = ObjectFromId[block.ObjectId].GetComponent<CullingZoneObject>();
			if (connector == null)
				continue;
			foreach (var id in (List<object>)connectedZonesObj)
			{
				if (!ObjectFromId.TryGetValue(Convert.ToInt32(id), out var target) ||
				    !target.TryGetComponent<CullingZoneObject>(out var zone))
				{
					continue;
				}
				connector.ConnectedZones.Add(zone);
			}
		}
	}


	public void Destroy() => Destroy(gameObject);

	private void OnDestroy()
	{
		// In case Destroy was called on SchematicObject instead of MapEditorObject.
		if (gameObject.TryGetComponent<MapEditorObject>(out var mapEditorObject))
		{
			IndicatorObject.TryDestroyIndicator(mapEditorObject);
			if (MapUtils.LoadedMaps.TryGetValue(mapEditorObject.MapName, out var loadedMap))
			{
				if (loadedMap.TryRemoveElement(mapEditorObject.Id))
					loadedMap.DestroyObject(mapEditorObject.Id);
			}
		}
		
		AnimationController.Dictionary.Remove(this);
		foreach (var obj in ObjectFromId.Values)
		{
			if (obj == null)
				continue;
			if (obj.parent == null)
				NetworkServer.Destroy(obj.gameObject);
		}
		ActionHostsByObjectId.Clear();
		ActionsByObjectId.Clear();
		NetworkServer.Destroy(gameObject);
		Schematic.OnSchematicDestroyed(new(this, Name));
	}

	public Dictionary<int, Transform> ObjectFromId = [];
	public Dictionary<int, ActionEventHostObject> ActionHostsByObjectId { get; } = [];
	public Dictionary<int, Dictionary<string, List<ActionGame>>> ActionsByObjectId { get; } = [];
	public Dictionary<int, AudioPlayerSettings> AudioPlayerSettingsByObjectId { get; } = [];
	
	private readonly List<GameObject> _attachedBlocks = [];
	private readonly List<NetworkIdentity> _networkIdentities = [];
	private readonly List<AdminToyBase> _adminToyBases = [];
	private readonly Dictionary<GameObject, RuntimeAnimatorController> _animators = [];
}
