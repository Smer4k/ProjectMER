using System.Text.RegularExpressions;
using HarmonyLib;
using LabApi.Features.Wrappers;
using MEC;
using ProjectMER.Events.Handlers.Internal;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using ProjectMER.Features.ToolGun;
using ProjectMER.HSM.Configs;

namespace ProjectMER.HSM.Patches;

[HarmonyPatch(typeof(ToolGunEventsHandler), "ToolGunGUI")]
internal static class ToolGunHudPatch
{
	private static readonly Regex SizeTagRegex = new("</?size[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	private static bool Prefix(ref IEnumerator<float> __result)
	{
		__result = ReplacementToolGunGui();
		return false;
	}

	private static IEnumerator<float> ReplacementToolGunGui()
	{
		while (true)
		{
			yield return Timing.WaitForSeconds(0.1f);

			Config config = Plugin.Instance!.Config!;

			foreach (Player player in Player.List)
			{
				if (!player.CurrentItem.IsToolGun(out ToolGunItem _) && !ToolGunHandler.TryGetSelectedMapObject(player, out MapEditorObject _))
					continue;

				string hud;
				try
				{
					string raw = ToolGunUI.GetHintHUD(player);
					string clean = SizeTagRegex.Replace(raw, string.Empty);
					hud = $"<line-height={config.LineHeight}><size=50%>{clean}</size></line-height>";
				}
				catch (Exception e)
				{
					Logger.Error(e);
					hud = "ERROR: Check server console";
				}

				player.SendHint(hud, 0.25f);
			}
		}
	}
}
