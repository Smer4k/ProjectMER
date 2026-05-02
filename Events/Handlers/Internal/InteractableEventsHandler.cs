using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using ProjectMER.Features.Actions;

namespace ProjectMER.Events.Handlers.Internal;

public sealed class InteractableEventsHandler : CustomEventsHandler
{
    public override void OnPlayerInteractedToy(PlayerInteractedToyEventArgs ev)
    {
        if (!ActionInteractableToy.Instances.TryGetValue(ev.Interactable, out ActionInteractableToy instance))
            return;

        if (ev.Interactable.IsLocked)
            return;

        if (!instance.CheckPermissions(ev.Player))
            return;

        instance.OnInteracted(ev.Player);
    }

    public override void OnPlayerSearchingToy(PlayerSearchingToyEventArgs ev)
    {
        if (!ActionInteractableToy.Instances.TryGetValue(ev.Interactable, out ActionInteractableToy instance))
            return;

        if (!instance.CheckPermissions(ev.Player))
        {
            ev.IsAllowed = false;
            instance.PermissionsRejected = true;
            return;
        }

        instance.OnSearching(ev.Player);
    }

    public override void OnPlayerSearchToyAborted(PlayerSearchToyAbortedEventArgs ev)
    {
        if (!ActionInteractableToy.Instances.TryGetValue(ev.Interactable, out ActionInteractableToy instance))
            return;

        if (instance.PermissionsRejected)
        {
            instance.PermissionsRejected = false;
            return;
        }
        
        instance.OnSearchAborted(ev.Player);
    }

    public override void OnPlayerSearchedToy(PlayerSearchedToyEventArgs ev)
    {
        if (!ActionInteractableToy.Instances.TryGetValue(ev.Interactable, out ActionInteractableToy instance))
            return;

        instance.OnSearched(ev.Player);
    }
}