using LabApi.Events.Arguments.Scp079Events;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using MapGeneration;
using NorthwoodLib.Pools;
using PlayerRoles.PlayableScps.Scp079;
using ProjectMER.Features;
using Scp2176Projectile = InventorySystem.Items.ThrowableProjectiles.Scp2176Projectile;

namespace ProjectMER.Events.Handlers.Internal;

public class FlickerEventsHandler : CustomEventsHandler
{
    public override void OnServerProjectileExploded(ProjectileExplodedEventArgs ev)
    {
        if (ev.TimedGrenade.Base is not Scp2176Projectile scp2176Projectile)
            return;

        if (ev.Position.TryGetRoom(out var room) &&
            FlickerController.FlickersByRoom.TryGetValue(room, out HashSet<FlickerController> flickers))
        {
            foreach (var flicker in flickers)
            {
                flicker.ServerFlickerLights(flicker.LightEnabled ? scp2176Projectile.LockdownDuration : 0.1f);
            }
        }

        flickers = FlickerController.GetFlickers(ev.Position, ProjectMER.Singleton.Config!.LightDisablingRadiusScp2176);
        foreach (var flicker in flickers)
        {
            flicker.ServerFlickerLights(flicker.LightEnabled ? scp2176Projectile.LockdownDuration : 0.1f);
        }

        HashSetPool<FlickerController>.Shared.Return(flickers);
    }

    public override void OnScp079BlackedOutZone(Scp079BlackedOutZoneEventArgs ev)
    {
        if (ev.Player.RoleBase is not Scp079Role scp079Role)
            return;
        if (!scp079Role.SubroutineModule.TryGetSubroutine<Scp079BlackoutZoneAbility>(out var ability))
            return;
        FlickerController.SetLightsByZone(ev.Zone, ability._duration);
    }

    public override void OnScp079BlackedOutRoom(Scp079BlackedOutRoomEventArgs ev)
    {
        if (ev.Player.RoleBase is not Scp079Role scp079Role)
            return;
        if (!scp079Role.SubroutineModule.TryGetSubroutine<Scp079BlackoutRoomAbility>(out var ability))
            return;
        if (!FlickerController.FlickersByRoom.TryGetValue(ev.Room.Base, out HashSet<FlickerController> flickers))
            return;
        foreach (var flicker in flickers)
        {
            flicker.ServerFlickerLights(ability._blackoutDuration);
        }
    }
}