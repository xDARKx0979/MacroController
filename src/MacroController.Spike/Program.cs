using MacroController.Core.Bindings;
using MacroController.Core.Hooks;
using MacroController.Core.Input;
using MacroController.Core.Macros;
using MacroController.Core.Storage;

const int VK_ESCAPE = 0x1B;
const int VK_F1 = 0x70;
const int VK_F3 = 0x72;
const int VK_F4 = 0x73;
const int VK_F9 = 0x78;
const int VK_F10 = 0x79;
const int VK_B = 0x42;
const int VK_C = 0x43;
const string MacroFilePath = "macro.json";

Console.WriteLine("MacroController core spike");
Console.WriteLine("  - F9 starts/stops recording a macro (key + mouse events with real timing).");
Console.WriteLine("  - F10 plays back the last recorded macro.");
Console.WriteLine("  - Bindings demo:");
Console.WriteLine("      F1            -> remapped to 'B'");
Console.WriteLine("      F2            -> plays back macro.json (if one exists)");
Console.WriteLine("      F3            -> types a text snippet");
Console.WriteLine("      F4            -> launches Notepad");
Console.WriteLine("      Mouse X1/back -> remapped to 'C'");
Console.WriteLine("  - ESC exits.");
Console.WriteLine();

using var keyboardHook = new KeyboardHook();
using var mouseHook = new MouseHook();
var recorder = new MacroRecorder();
var bindingManager = new BindingManager();

var bindings = new List<Binding>
{
    new(new Trigger(InputDevice.Keyboard, VK_F1), new RemapAction(VK_B)),
    new(new Trigger(InputDevice.Keyboard, VK_F3), new TextAction("Hello from MacroController!")),
    new(new Trigger(InputDevice.Keyboard, VK_F4), new LaunchAppAction("notepad.exe")),
    new(new Trigger(InputDevice.Mouse, (int)MouseButton.X1), new RemapAction(VK_C)),
};

if (File.Exists(MacroFilePath))
{
    const int VK_F2 = 0x71;
    var savedMacro = MacroStore.Load(MacroFilePath);
    bindings.Add(new Binding(new Trigger(InputDevice.Keyboard, VK_F2), new MacroAction(savedMacro)));
    Console.WriteLine($"Loaded '{savedMacro.Name}' from {MacroFilePath} -> bound to F2");
}
else
{
    Console.WriteLine($"No {MacroFilePath} found - F2 binding skipped (record one with F9 first).");
}

bindingManager.SetBindings(bindings);
Console.WriteLine();

keyboardHook.KeyDown += (_, e) =>
{
    switch (e.VirtualKeyCode)
    {
        case VK_ESCAPE:
            Console.WriteLine("ESC pressed, exiting.");
            Environment.Exit(0);
            return;
        case VK_F9:
            ToggleRecording();
            return;
        case VK_F10:
            PlayLastMacro();
            return;
    }

    if (bindingManager.HandleDown(new Trigger(InputDevice.Keyboard, e.VirtualKeyCode)))
    {
        e.Handled = true;
        return;
    }

    Log("KeyDown", KeyNames.GetName(e.ScanCode, e.IsExtended));
    recorder.RecordKey(e.VirtualKeyCode, ActionType.KeyDown);
};

keyboardHook.KeyUp += (_, e) =>
{
    if (e.VirtualKeyCode is VK_ESCAPE or VK_F9 or VK_F10)
        return;

    if (bindingManager.HandleUp(new Trigger(InputDevice.Keyboard, e.VirtualKeyCode)))
    {
        e.Handled = true;
        return;
    }

    Log("KeyUp", KeyNames.GetName(e.ScanCode, e.IsExtended));
    recorder.RecordKey(e.VirtualKeyCode, ActionType.KeyUp);
};

mouseHook.MouseDown += (_, e) =>
{
    if (bindingManager.HandleDown(new Trigger(InputDevice.Mouse, (int)e.Button)))
    {
        e.Handled = true;
        return;
    }

    Log("MouseDown", e.Button.ToString());
    recorder.RecordMouseButton(e.Button, ActionType.MouseDown);
};

mouseHook.MouseUp += (_, e) =>
{
    if (bindingManager.HandleUp(new Trigger(InputDevice.Mouse, (int)e.Button)))
    {
        e.Handled = true;
        return;
    }

    Log("MouseUp", e.Button.ToString());
    recorder.RecordMouseButton(e.Button, ActionType.MouseUp);
};

mouseHook.MouseWheel += (_, e) =>
{
    Log("MouseWheel", $"delta={e.Delta}");
    recorder.RecordWheel(e.Delta);
};

keyboardHook.Install();
mouseHook.Install();
MessageLoop.Run();

void Log(string kind, string detail) =>
    Console.WriteLine($"{(recorder.IsRecording ? "[REC] " : "")}{kind,-10} {detail}");

void ToggleRecording()
{
    if (!recorder.IsRecording)
    {
        Console.WriteLine(">> Recording started (F9 to stop)");
        recorder.Start();
        return;
    }

    var macro = recorder.Stop("Recorded Macro");
    MacroStore.Save(macro, MacroFilePath);
    Console.WriteLine($">> Recording stopped: {macro.Steps.Count} steps saved to {MacroFilePath}");
}

void PlayLastMacro()
{
    if (!File.Exists(MacroFilePath))
    {
        Console.WriteLine(">> No saved macro found - record one with F9 first.");
        return;
    }

    var macro = MacroStore.Load(MacroFilePath);
    Console.WriteLine($">> Playing back '{macro.Name}' ({macro.Steps.Count} steps)...");

    _ = Task.Run(async () =>
    {
        await MacroPlayer.PlayAsync(macro);
        Console.WriteLine(">> Playback finished.");
    });
}
