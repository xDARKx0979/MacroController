using System.Text;
using MacroController.Core.Native;

namespace MacroController.Core.Hooks;

/// <summary>Resolves a scan code to its localized display name (e.g. "A", "Left Ctrl", "Num Lock").</summary>
public static class KeyNames
{
    public static string GetName(int scanCode, bool isExtended)
    {
        int lParam = (scanCode << 16) | (isExtended ? 1 << 24 : 0);

        var buffer = new StringBuilder(64);
        int length = NativeMethods.GetKeyNameText(lParam, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString() : $"scan 0x{scanCode:X2}";
    }

    /// <summary>Resolves a virtual-key code (e.g. a remap target) to its display name.</summary>
    public static string GetNameFromVirtualKey(int virtualKeyCode)
    {
        int scanCode = (int)NativeMethods.MapVirtualKey((uint)virtualKeyCode, NativeMethods.MAPVK_VK_TO_VSC);
        return GetName(scanCode, isExtended: false);
    }
}
