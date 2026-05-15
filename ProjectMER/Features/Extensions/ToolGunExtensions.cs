using InventorySystem.Items;
using LabApi.Features.Wrappers;
using ProjectMER.Features.ToolGun;

namespace ProjectMER.Features.Extensions;

public static class ToolGunExtensions
{
    public static bool IsToolGun(this Item? item, out ToolGunItem toolGun)
    {
		if (item == null)
		{
			toolGun = null!;
			return false;
		}

        return IsToolGun(item.Base, out toolGun);
    }

    public static bool IsToolGun(this ItemBase? itemBase, out ToolGunItem toolGun)
    {
		if (itemBase == null)
		{
			toolGun = null!;
			return false;
		}

        return ToolGunItem.ItemDictionary.TryGetValue(itemBase.ItemSerial, out toolGun);
    }
}
