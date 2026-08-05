using System.Diagnostics;
using MacroController.Core.Input;
using MacroController.Core.Storage;

namespace MacroController.Core.Macros;

/// <summary>Replays a recorded <see cref="Macro"/> via <see cref="InputSender"/>.</summary>
public static class MacroPlayer
{
    public static async Task PlayAsync(Macro macro, CancellationToken cancellationToken = default)
    {
        var heldInputs = new HashSet<(InputDevice Device, int Code)>();

        try
        {
            await PlayAsync(macro, cancellationToken, new HashSet<string>(), heldInputs);
        }
        finally
        {
            // If playback was cancelled (or threw) between a KeyDown/MouseDown and its
            // matching Up, release whatever's still "held" so it doesn't get stuck down
            // at the OS level and block/skew the user's own input afterwards.
            foreach (var (device, code) in heldInputs)
            {
                if (device == InputDevice.Keyboard)
                    InputSender.SendKeyUp(code);
                else
                    InputSender.SendMouseButtonUp((MouseButton)code);
            }
        }
    }

    /// <summary><paramref name="visiting"/> guards against infinite recursion from <see cref="ActionType.CallMacro"/> cycles.</summary>
    private static async Task PlayAsync(Macro macro, CancellationToken cancellationToken, HashSet<string> visiting, HashSet<(InputDevice Device, int Code)> heldInputs)
    {
        if (!visiting.Add(macro.Id))
            return;

        try
        {
            await PlayStepsAsync(macro, cancellationToken, visiting, heldInputs);
        }
        finally
        {
            visiting.Remove(macro.Id);
        }
    }

    private static async Task PlayStepsAsync(Macro macro, CancellationToken cancellationToken, HashSet<string> visiting, HashSet<(InputDevice Device, int Code)> heldInputs)
    {
        var steps = macro.Steps;
        var random = new Random();

        // Stack of (index of the LoopStart, iterations remaining including this one).
        // A stack lets nested loops jump back to the correct LoopStart on each LoopEnd.
        var loops = new Stack<(int StartIndex, int Remaining)>();

        int i = 0;
        while (i < steps.Count)
        {
            var step = steps[i];

            if (step.Action == ActionType.LoopStart)
            {
                loops.Push((i, Math.Max(1, step.Code)));
                i++;
                continue;
            }

            if (step.Action == ActionType.LoopEnd)
            {
                if (loops.Count > 0)
                {
                    var (startIndex, remaining) = loops.Pop();
                    if (remaining > 1)
                    {
                        loops.Push((startIndex, remaining - 1));
                        i = startIndex + 1;
                        continue;
                    }
                }

                i++;
                continue;
            }

            int delayMs = GetDelayMs(macro, step, random);
            await PrecisionDelay.WaitAsync(delayMs, cancellationToken);

            switch (step.Action)
            {
                case ActionType.Delay:
                    // The wait already happened above; nothing else to do.
                    break;
                case ActionType.LaunchApp:
                    Process.Start(new ProcessStartInfo(step.Text ?? "")
                    {
                        Arguments = step.Text2 ?? string.Empty,
                        UseShellExecute = true,
                    });
                    break;
                case ActionType.RunCommand:
                    Process.Start(new ProcessStartInfo("cmd.exe", $"/c {step.Text}")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                    break;
                case ActionType.TypeText:
                    InputSender.SendText(step.Text ?? string.Empty);
                    break;
                case ActionType.CallMacro:
                    if (step.Text is { } macroId && MacroLibraryStore.FindById(macroId) is { } called)
                        await PlayAsync(called, cancellationToken, visiting, heldInputs);
                    break;
                case ActionType.KeyDown when step.Device == InputDevice.Keyboard:
                    InputSender.SendKeyDown(step.Code);
                    heldInputs.Add((InputDevice.Keyboard, step.Code));
                    break;
                case ActionType.KeyUp when step.Device == InputDevice.Keyboard:
                    InputSender.SendKeyUp(step.Code);
                    heldInputs.Remove((InputDevice.Keyboard, step.Code));
                    break;
                case ActionType.MouseDown when step.Device == InputDevice.Mouse:
                    InputSender.SendMouseButtonDown((MouseButton)step.Code);
                    heldInputs.Add((InputDevice.Mouse, step.Code));
                    break;
                case ActionType.MouseUp when step.Device == InputDevice.Mouse:
                    InputSender.SendMouseButtonUp((MouseButton)step.Code);
                    heldInputs.Remove((InputDevice.Mouse, step.Code));
                    break;
                case ActionType.Wheel when step.Device == InputDevice.Mouse:
                    InputSender.SendMouseWheel(step.Code);
                    break;
                case ActionType.HWheel when step.Device == InputDevice.Mouse:
                    InputSender.SendMouseHWheel(step.Code);
                    break;
            }

            i++;
        }
    }

    private static int GetDelayMs(Macro macro, InputEvent step, Random random) => macro.DelayMode switch
    {
        MacroDelayMode.Fixed => macro.StandardDelayMs,
        MacroDelayMode.Randomized => random.Next(macro.RandomDelayMinMs, macro.RandomDelayMaxMs + 1),
        MacroDelayMode.None => 0,
        _ => step.DelayMs,
    };
}
