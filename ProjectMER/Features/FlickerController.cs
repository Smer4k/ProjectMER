using MapGeneration;
using NorthwoodLib.Pools;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using UnityEngine;
using LightSourceToy = AdminToys.LightSourceToy;

namespace ProjectMER.Features;

public class FlickerController : MonoBehaviour
{
    public static readonly List<FlickerController> Instances = new();
    public static readonly Dictionary<RoomIdentifier, HashSet<FlickerController>> FlickersByRoom = new();
    public static readonly Dictionary<SchematicObject, HashSet<FlickerController>> FlickersBySchematic = new();
    public LightSourceToy LightSourceToy { get; private set; }
    public RoomIdentifier? Room { get; private set; }
    public SchematicObject? Schematic { get; private set; }
    public FacilityZone Zone = FacilityZone.None;
    public bool LightEnabled => _lightEnabled;

    private float _prevLightRange;
    private float _flickerDuration;
    private bool _lightEnabled = true;
    private bool _roomAssigned;
    private bool _schematicAssigned;

    public void Start()
    {
        LightSourceToy = GetComponent<LightSourceToy>();
        if (LightSourceToy == null)
        {
            Logger.Error("FlickerController: No LightSourceToy found! Destroying FlickerController!");
            Destroy(this);
            return;
        }

        Instances.Add(this);
    }
    
    public void OnDestroy()
    {
        Instances.Remove(this);
        if (_roomAssigned && FlickersByRoom.TryGetValue(Room!, out HashSet<FlickerController> flickers))
        {
            flickers.Remove(this);
            _roomAssigned = false;
            if (flickers.Count == 0)
            {
                FlickersByRoom.Remove(Room!);
            }
            Room = null;
        }

        if (_schematicAssigned && FlickersBySchematic.TryGetValue(Schematic!, out var set))
        {
            set.Remove(this);
            _schematicAssigned = false;
            if (set.Count != 0)
                return;
            FlickersBySchematic.Remove(Schematic!);
            Schematic = null;
        }
    }

    public void Update()
    {
        if (_flickerDuration <= 0.0)
            return;
        _flickerDuration -= Time.deltaTime;
        if (_flickerDuration > 0.0)
            return;
        SetLights(true);
    }

    public void ServerFlickerLights(float dur)
    {
        if (dur <= 0.0)
        {
            _flickerDuration = 0.0f;
            SetLights(true);
        }
        else
        {
            _flickerDuration = dur;
            SetLights(false);
        }
    }

    public void SetLights(bool state)
    {
        if (state)
        {
            if (_lightEnabled)
                return;
            _lightEnabled = true;
            LightSourceToy?.NetworkLightRange = _prevLightRange;
        }
        else
        {
            if (!_lightEnabled)
                return;
            _lightEnabled = false;
            _prevLightRange = LightSourceToy.NetworkLightRange;
            LightSourceToy?.NetworkLightRange = 0.0f;
        }
    }

    public void UpdateRoom(RoomIdentifier? room)
    {
        if (_roomAssigned && FlickersByRoom.TryGetValue(Room!, out HashSet<FlickerController> flickers))
        {
            flickers.Remove(this);
            _roomAssigned = false;
        }

        if (room != null)
        {
            if (FlickersByRoom.TryGetValue(room, out flickers))
                flickers.Add(this);
            else
            {
                HashSet<FlickerController> flickerControllers = [this];
                FlickersByRoom.Add(room, flickerControllers);
            }

            _roomAssigned = true;
        }

        Room = room;
    }

    public static HashSet<FlickerController> GetFlickers(Vector3 position, float distance,
        bool includeRooms = false)
    {
        var flickers = HashSetPool<FlickerController>.Shared.Rent();

        foreach (var flicker in Instances)
        {
            var distanceSqr = (flicker.transform.position - position).sqrMagnitude;
            if (distanceSqr > distance * distance)
                continue;
            if (flicker._roomAssigned && !includeRooms)
                continue;
            flickers.Add(flicker);
        }

        return flickers;
    }

    public static void SetLightsByZone(FacilityZone zoneToAffect, float duration)
    {
        var all = zoneToAffect == FacilityZone.None;
        foreach (var flicker in Instances)
        {
            if (all || flicker.Zone == zoneToAffect)
            {
                flicker.ServerFlickerLights(duration);
                continue;
            }

            if (flicker.Zone != FacilityZone.None) 
                continue;
            if (flicker.transform.position.TryGetRoom(out var identifier) && identifier.Zone == zoneToAffect)
                flicker.ServerFlickerLights(duration);
        }
    }

    public static void OnSchematicUpdate(GameObject gameObject)
    {
        if (!gameObject.TryGetComponent(out SchematicObject schematic))
            return;
        if (!FlickersBySchematic.TryGetValue(schematic, out HashSet<FlickerController> flickerControllers))
            return;

        foreach (var flicker in flickerControllers)
        {
            flicker.transform.position.TryGetRoom(out var room);
            flicker.UpdateRoom(room);
        }
    }

    public void AddSchematic(SchematicObject schematic)
    {
        if (schematic == null)
            return;
        if (_schematicAssigned)
            return;
        Schematic = schematic;
        _schematicAssigned = true;
        if (FlickersBySchematic.TryGetValue(schematic, out HashSet<FlickerController> flickerControllers))
            flickerControllers.Add(this);
        else
        {
            flickerControllers = [this];
            FlickersBySchematic.Add(schematic, flickerControllers);
        }
    }
}