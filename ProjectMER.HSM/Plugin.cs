global using Logger = LabApi.Features.Console.Logger;

using HarmonyLib;
using LabApi.Features;
using LabApi.Loader.Features.Plugins;
using ProjectMER.HSM.Configs;

namespace ProjectMER.HSM;

public class Plugin : Plugin<Config>
{
	private Harmony? _harmony;

	public static Plugin? Instance { get; private set; }

	public override void Enable()
	{
		Instance = this;

		_harmony = new Harmony($"projectmer.hsm-{DateTime.Now.Ticks}");
		_harmony.PatchAll();
	}

	public override void Disable()
	{
		Instance = null;

		_harmony?.UnpatchAll();
		_harmony = null;
	}

	public override string Name => "ProjectMER.HSM";

	public override string Description => "Cleans up ProjectMER's ToolGun HUD for HintServiceMeow.";

	public override string Author => "DiabeloDEV";

	public override Version Version => new(2026, 8, 18, 1);

	public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);
}
