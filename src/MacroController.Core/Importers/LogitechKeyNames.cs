namespace MacroController.Core.Importers;

/// <summary>
/// Maps the key-name strings used by Logitech Gaming Software / G HUB keystroke macros
/// (e.g. "LeftControl", "F5", "OemComma") to Win32 virtual-key codes.
/// </summary>
internal static class LogitechKeyNames
{
    private static readonly Dictionary<string, int> Map = BuildMap();

    /// <summary>Resolves a Logitech key name to a virtual-key code, or null if it has no equivalent.</summary>
    public static int? ToVirtualKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        string normalized = Normalize(key);
        return Map.TryGetValue(normalized, out int vk) ? vk : null;
    }

    private static string Normalize(string key)
    {
        // Strip common prefixes ("VK_", "Key_") and separators so "VK_LCONTROL",
        // "Key_LeftControl" and "LeftControl" all match the same entry.
        string s = key.Trim();
        if (s.StartsWith("VK_", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Key_", StringComparison.OrdinalIgnoreCase))
            s = s[(s.IndexOf('_') + 1)..];

        return s.Replace("_", "").Replace(" ", "").ToLowerInvariant();
    }

    private static Dictionary<string, int> BuildMap()
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        void Add(int vk, params string[] names)
        {
            foreach (var name in names)
                map[Normalize(name)] = vk;
        }

        for (char c = 'A'; c <= 'Z'; c++)
            Add(c, c.ToString());

        for (char c = '0'; c <= '9'; c++)
            Add(c, c.ToString(), "D" + c, "Digit" + c);

        for (int f = 1; f <= 24; f++)
            Add(0x6F + f, "F" + f); // F1 = 0x70

        Add(0x08, "Backspace");
        Add(0x09, "Tab");
        Add(0x0D, "Enter", "Return");
        Add(0x10, "Shift");
        Add(0x11, "Control", "Ctrl");
        Add(0x12, "Alt", "Menu");
        Add(0x13, "Pause");
        Add(0x14, "CapsLock");
        Add(0x1B, "Escape", "Esc");
        Add(0x20, "Space", "Spacebar");
        Add(0x21, "PageUp", "Prior");
        Add(0x22, "PageDown", "Next");
        Add(0x23, "End");
        Add(0x24, "Home");
        Add(0x25, "Left", "LeftArrow");
        Add(0x26, "Up", "UpArrow");
        Add(0x27, "Right", "RightArrow");
        Add(0x28, "Down", "DownArrow");
        Add(0x2C, "PrintScreen", "Snapshot");
        Add(0x2D, "Insert");
        Add(0x2E, "Delete", "Del");
        Add(0x5B, "LeftWindows", "LWin", "Win");
        Add(0x5C, "RightWindows", "RWin");
        Add(0x5D, "Apps", "Menu_");

        Add(0x60, "Numpad0");
        Add(0x61, "Numpad1");
        Add(0x62, "Numpad2");
        Add(0x63, "Numpad3");
        Add(0x64, "Numpad4");
        Add(0x65, "Numpad5");
        Add(0x66, "Numpad6");
        Add(0x67, "Numpad7");
        Add(0x68, "Numpad8");
        Add(0x69, "Numpad9");
        Add(0x6A, "NumpadMultiply", "Multiply");
        Add(0x6B, "NumpadAdd", "Add");
        Add(0x6C, "Separator");
        Add(0x6D, "NumpadSubtract", "Subtract");
        Add(0x6E, "NumpadDecimal", "Decimal");
        Add(0x6F, "NumpadDivide", "Divide");

        Add(0x90, "NumLock");
        Add(0x91, "ScrollLock");

        Add(0xA0, "LeftShift", "LShift");
        Add(0xA1, "RightShift", "RShift");
        Add(0xA2, "LeftControl", "LCtrl", "LControl");
        Add(0xA3, "RightControl", "RCtrl", "RControl");
        Add(0xA4, "LeftAlt", "LAlt", "LMenu");
        Add(0xA5, "RightAlt", "RAlt", "RMenu");

        Add(0xBA, "Semicolon", "OemSemicolon", "Oem1");
        Add(0xBB, "Equals", "OemPlus", "Plus");
        Add(0xBC, "Comma", "OemComma");
        Add(0xBD, "Minus", "OemMinus");
        Add(0xBE, "Period", "OemPeriod");
        Add(0xBF, "Slash", "OemQuestion", "Oem2");
        Add(0xC0, "Grave", "Tilde", "OemTilde", "Oem3");
        Add(0xDB, "OpenBracket", "OemOpenBrackets", "Oem4", "LeftBracket");
        Add(0xDC, "Backslash", "OemBackslash", "Oem5", "Pipe");
        Add(0xDD, "CloseBracket", "OemCloseBrackets", "Oem6", "RightBracket");
        Add(0xDE, "Quote", "OemQuotes", "Oem7", "Apostrophe");

        return map;
    }
}
