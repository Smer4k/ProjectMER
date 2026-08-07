using HarmonyLib;
using MapGeneration.Distributors;
using PlayerRoles.PlayableScps.Scp079;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectMER.Patches;

[HarmonyPatch(typeof(Scp079Generator), nameof(Scp079Generator.OnDestroy))]
public static class Scp079GeneratorPatch
{
    public static void Postfix(Scp079Generator __instance)
    {
        foreach (var recontainer in Object.FindObjectsByType<Scp079Recontainer>(FindObjectsSortMode.None))
        {
            recontainer._prevEngaged = 0;
        }
    }
}