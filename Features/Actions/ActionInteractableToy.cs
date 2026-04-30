using LabApi.Features.Wrappers;
using MEC;
using ProjectMER.Features.Objects;

namespace ProjectMER.Features.Actions;

public sealed class ActionInteractableToy
{
    public static readonly List<ActionInteractableToy> Instances = new List<ActionInteractableToy>();

    public readonly InteractableToy Toy;
    public readonly SchematicObject SchematicObject;
    public readonly int ObjectId;

    private static CoroutineHandle _coroutineCheckInstances;

    public ActionInteractableToy(int objectId, InteractableToy toy, SchematicObject schematicObject)
    {
        if (schematicObject.TryGetActionsByEventId(objectId, nameof(OnInteracted), out _))
        {
            toy.OnInteracted += OnInteracted;
        }

        if (schematicObject.TryGetActionsByEventId(objectId, nameof(OnSearchAborted), out _))
        {
            toy.Base.OnSearchAborted += OnSearchAborted;
        }

        if (schematicObject.TryGetActionsByEventId(objectId, nameof(OnSearching), out _))
        {
            toy.OnSearching += OnSearching;
        }

        if (schematicObject.TryGetActionsByEventId(objectId, nameof(OnSearched), out _))
        {
            toy.OnSearched += OnSearched;
        }


        Toy = toy;
        SchematicObject = schematicObject;
        ObjectId = objectId;
    }

    public static void Register(int objectId, InteractableToy toy, SchematicObject schematicObject)
    {
        ActionInteractableToy? actionInteractableToy = null;

        if (schematicObject.TryGetActionsByEventId(objectId, nameof(OnInteracted), out _) ||
            schematicObject.TryGetActionsByEventId(objectId, nameof(OnSearching), out _) ||
            schematicObject.TryGetActionsByEventId(objectId, nameof(OnSearched), out _) ||
            schematicObject.TryGetActionsByEventId(objectId, nameof(OnSearchAborted), out _))
        {
            actionInteractableToy = new(objectId, toy, schematicObject);
        }
        
        if (actionInteractableToy == null)
            return;

        Instances.Add(actionInteractableToy);
        if (!_coroutineCheckInstances.IsRunning || !_coroutineCheckInstances.IsValid)
            _coroutineCheckInstances = Timing.RunCoroutine(CheckInstances());
    }

    private static IEnumerator<float> CheckInstances()
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(10f);

            for (var i = 0; i < Instances.Count; i++)
            {
                yield return Timing.WaitForOneFrame;

                if (Instances[i].Toy.Base != null)
                    continue;
                Instances[i].Unregister();
                Instances.RemoveAt(i);
                i--;
            }

            if (Instances.Count == 0)
                break;
        }
    }

    private void OnInteracted(Player player)
    {
        if (Toy.IsLocked)
            return;
        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnInteracted), player);
    }

    private void OnSearchAborted(ReferenceHub hub)
    {
        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnSearchAborted), Player.Get(hub));
    }

    private void OnSearching(Player player)
    {
        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnSearching), player);
    }

    private void OnSearched(Player player)
    {
        SchematicObject.RunActionsByEventId(ObjectId, nameof(OnSearched), player);
    }

    public void Unregister()
    {
        Toy.OnInteracted -= OnInteracted;
        if (Toy.Base != null)
            Toy.Base.OnSearchAborted -= OnSearchAborted;
        Toy.OnSearching -= OnSearching;
        Toy.OnSearched -= OnSearched;
    }
}