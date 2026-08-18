using System.ComponentModel;

namespace ProjectMER.HSM.Configs;

public class Config
{
	[Description("Line height (px) for the ToolGun HUD text.")]
	public int LineHeight { get; set; } = 22;
}
