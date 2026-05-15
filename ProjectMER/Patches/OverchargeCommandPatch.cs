using CommandSystem.Commands.RemoteAdmin;
using HarmonyLib;
using MapGeneration;
using ProjectMER.Features;

namespace ProjectMER.Patches;

[HarmonyPatch(typeof(OverchargeCommand), nameof(OverchargeCommand.Overcharge))]
public static class OverchargeCommandPatch
{
    public static bool Prefix(FacilityZone zoneToAffect, float duration)
    {
        FlickerController.SetLightsByZone(zoneToAffect, duration);
        return true;
    }
}