using AdminToys;
using LabApi.Features.Wrappers;
using ProjectMER.Features.Actions;

namespace ProjectMER.Events.Handlers.Internal;

public static class AdminToysHandler
{
    public static void Register()
    {
        AdminToyBase.OnRemoved += OnRemoved;
    }

    public static void Unregister()
    {
        AdminToyBase.OnRemoved -= OnRemoved;
    }

    private static void OnRemoved(AdminToyBase toyBase)
    {
        if (toyBase is not InvisibleInteractableToy interactableToy)
            return;
        var toy = InteractableToy.Get(interactableToy);
        if (toy == null)
        {
            Logger.Warn($"Toy is null");
            return;
        }

        ActionInteractableToy.Instances.Remove(toy);
    }
}