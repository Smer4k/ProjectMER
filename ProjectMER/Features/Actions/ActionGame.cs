using ProjectMER.Features.Enums;
using UnityEngine;

namespace ProjectMER.Features.Actions;

public class ActionGame
{
	public ActionType Type { get; set; }
	public float ActionDelay { get; set; }
	public string Value { get; set; } = string.Empty;
	
	public int TargetId { get; set; }
	
	public BlockType BlockType { get; set; }

	public string Param { get; set; } = string.Empty;

	public AnimatorControllerParameterType ParamType { get; set; }

	public void EnsureDefaults()
	{
		Value ??= string.Empty;
		Param ??= string.Empty;
	}
}
