using MacroController.Core.Input;

namespace MacroController.Core.Macros;

/// <summary>Replays a recorded <see cref="Macro"/> via <see cref="InputSender"/>.</summary>
public static class MacroPlayer
{
    public static async Task PlayAsync(Macro macro, CancellationToken cancellationToken = default)
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

            switch (step.Device, step.Action)
            {
                case (InputDevice.Keyboard, ActionType.KeyDown):
                    InputSender.SendKeyDown(step.Code);
                    break;
                case (InputDevice.Keyboard, ActionType.KeyUp):
                    InputSender.SendKeyUp(step.Code);
                    break;
                case (InputDevice.Mouse, ActionType.MouseDown):
                    InputSender.SendMouseButtonDown((MouseButton)step.Code);
                    break;
                case (InputDevice.Mouse, ActionType.MouseUp):
                    InputSender.SendMouseButtonUp((MouseButton)step.Code);
                    break;
                case (InputDevice.Mouse, ActionType.Wheel):
                    InputSender.SendMouseWheel(step.Code);
                    break;
                case (InputDevice.Mouse, ActionType.HWheel):
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
