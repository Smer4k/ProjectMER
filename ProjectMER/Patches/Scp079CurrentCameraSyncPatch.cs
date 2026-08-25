using System.Reflection.Emit;
using HarmonyLib;
using PlayerRoles.PlayableScps.Scp079.Cameras;
using ProjectMER.Features.Objects;

namespace ProjectMER.Patches;

[HarmonyPatch(typeof(Scp079CurrentCameraSync), nameof(Scp079CurrentCameraSync.ServerProcessCmd))]
public static class Scp079CurrentCameraSyncPatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        ILGenerator generator)
    {
        var matcher = new CodeMatcher(instructions, generator).Start();

        var requestedCamIdField = AccessTools.Field(typeof(Scp079CurrentCameraSync),
            nameof(Scp079CurrentCameraSync._requestedCamId));

        var getCameraByTransfer = AccessTools.Method(typeof(CameraTransferObject),
            nameof(CameraTransferObject.GetCameraByTransfer), new[] { typeof(ushort) });
        
        try
        {
            matcher
                .MatchStartForward(
                    new CodeMatch(i => i.StoresField(requestedCamIdField))
                )
                .ThrowIfNotMatch("pattern for this._requestedCamId = reader.ReadUShort(); not found")
                .Advance(1)
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, requestedCamIdField),
                    new CodeInstruction(OpCodes.Call, getCameraByTransfer),
                    new CodeInstruction(OpCodes.Stfld, requestedCamIdField)
                );
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString());
            return matcher.Instructions();
        }

        return matcher.Instructions();
    }
}