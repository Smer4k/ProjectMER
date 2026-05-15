using UnityEngine;
using UserSettings.ServerSpecific;

namespace ProjectMER.Features.Extensions;

public static class SSDropdownSettingExtensions
{
	public static bool TryGetSyncSelectionText(this SSDropdownSetting dropdownSetting, out string text)
	{
		if (dropdownSetting.OriginalDefinition is not SSDropdownSetting original)
		{
			text = string.Empty;
			return false;
		}

		int max = original.Options.Length - 1;
		int num = Mathf.Clamp(dropdownSetting.SyncSelectionIndexRaw, 0, max);
		return original.Options.TryGet(num, out text);
	}
}
