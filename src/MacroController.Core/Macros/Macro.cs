using MacroController.Core.Bindings;
using MacroController.Core.Input;

namespace MacroController.Core.Macros;

/// <summary>How a macro plays back when its trigger is pressed (mirrors Razer Synapse's playback options).</summary>
public enum MacroPlayMode
{
    /// <summary>Plays through once per press.</summary>
    NoRepeat,

    /// <summary>Repeats from the start for as long as the trigger is held down.</summary>
    RepeatWhileHeld,

    /// <summary>Starts repeating on press; a second press stops it.</summary>
    Toggle,

    /// <summary>Plays through <see cref="Macro.RepeatCount"/> times then stops.</summary>
    RepeatCount,
}

/// <summary>How the delay before each step is determined during playback.</summary>
public enum MacroDelayMode
{
    /// <summary>Use each step's recorded <see cref="InputEvent.DelayMs"/>.</summary>
    Recorded,

    /// <summary>Use <see cref="Macro.StandardDelayMs"/> for every step.</summary>
    Fixed,

    /// <summary>Use a random value in [<see cref="Macro.RandomDelayMinMs"/>, <see cref="Macro.RandomDelayMaxMs"/>] for every step.</summary>
    Randomized,

    /// <summary>No delay between steps.</summary>
    None,
}

public class Macro
{
    /// <summary>Stable identifier used to reference this macro from a <see cref="MacroController.Core.Input.ActionType.CallMacro"/> step.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "New Macro";

    public List<InputEvent> Steps { get; set; } = new();

    /// <summary>Optional global hotkey that plays this macro when pressed, regardless of the active profile.</summary>
    public Trigger? Shortcut { get; set; }

    public MacroPlayMode PlayMode { get; set; } = MacroPlayMode.NoRepeat;

    /// <summary>Number of times to play through when <see cref="PlayMode"/> is <see cref="MacroPlayMode.RepeatCount"/>.</summary>
    public int RepeatCount { get; set; } = 1;

    /// <summary>How the delay before each step is determined during playback.</summary>
    public MacroDelayMode DelayMode { get; set; } = MacroDelayMode.Recorded;

    /// <summary>Delay (ms) used for every step when <see cref="DelayMode"/> is <see cref="MacroDelayMode.Fixed"/>.</summary>
    public int StandardDelayMs { get; set; } = 50;

    /// <summary>Minimum delay (ms) used when <see cref="DelayMode"/> is <see cref="MacroDelayMode.Randomized"/>.</summary>
    public int RandomDelayMinMs { get; set; } = 20;

    /// <summary>Maximum delay (ms) used when <see cref="DelayMode"/> is <see cref="MacroDelayMode.Randomized"/>.</summary>
    public int RandomDelayMaxMs { get; set; } = 100;
}
