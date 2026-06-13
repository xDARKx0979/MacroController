using System.Diagnostics;
using MacroController.Core.Input;
using MacroController.Core.Macros;

namespace MacroController.Core.Bindings;

/// <summary>
/// Holds the active set of trigger -> action bindings and decides whether a given
/// input event should be swallowed. <see cref="HandleDown"/>/<see cref="HandleUp"/>
/// are the hot path called from the hook callbacks - they only do a dictionary
/// lookup and, for non-remap actions, queue work onto a background task.
/// </summary>
public sealed class BindingManager
{
    private readonly Dictionary<Trigger, Binding> _bindings = new();
    private readonly HashSet<Trigger> _heldTriggers = new();
    private readonly Dictionary<Trigger, CancellationTokenSource> _runningMacros = new();

    public void SetBindings(IEnumerable<Binding> bindings)
    {
        foreach (var cts in _runningMacros.Values)
            cts.Cancel();
        _runningMacros.Clear();

        _bindings.Clear();
        _heldTriggers.Clear();
        foreach (var binding in bindings)
            _bindings[binding.Trigger] = binding;
    }

    /// <returns>true if this key/button press is bound and should be swallowed.</returns>
    public bool HandleDown(Trigger trigger)
    {
        if (!_bindings.TryGetValue(trigger, out var binding))
            return false;

        bool isRepeat = !_heldTriggers.Add(trigger);

        if (binding.Action is RemapAction remap)
        {
            // Forward every down, including OS auto-repeats, so the target key
            // streams the same way it would if it were physically held. The
            // matching key-up is sent once, from HandleUp, on release.
            InputSender.SendKeyDown(remap.TargetVirtualKeyCode);
        }
        else if (binding.Action is MacroAction macroAction)
        {
            if (!isRepeat)
                HandleMacroPress(trigger, macroAction.Macro);
        }
        else if (!isRepeat)
        {
            // Text/launch actions fire once per physical press.
            Fire(binding.Action);
        }

        return true;
    }

    /// <returns>true if this key/button release is bound and should be swallowed.</returns>
    public bool HandleUp(Trigger trigger)
    {
        if (!_bindings.TryGetValue(trigger, out var binding))
            return false;

        _heldTriggers.Remove(trigger);

        if (binding.Action is RemapAction remap)
        {
            InputSender.SendKeyUp(remap.TargetVirtualKeyCode);
        }
        else if (binding.Action is MacroAction { Macro.PlayMode: MacroPlayMode.RepeatWhileHeld }
                 && _runningMacros.Remove(trigger, out var cts))
        {
            cts.Cancel();
        }

        return true;
    }

    /// <summary>Starts/stops a macro according to its <see cref="MacroPlayMode"/>.</summary>
    private void HandleMacroPress(Trigger trigger, Macro macro)
    {
        switch (macro.PlayMode)
        {
            case MacroPlayMode.RepeatWhileHeld:
                StartLoop(trigger, macro);
                break;

            case MacroPlayMode.Toggle:
                if (_runningMacros.Remove(trigger, out var existing))
                    existing.Cancel();
                else
                    StartLoop(trigger, macro);
                break;

            case MacroPlayMode.RepeatCount:
                if (!_runningMacros.ContainsKey(trigger))
                    StartRepeatCount(trigger, macro);
                break;

            default:
                _ = Task.Run(() => MacroPlayer.PlayAsync(macro));
                break;
        }
    }

    private void StartRepeatCount(Trigger trigger, Macro macro)
    {
        var cts = new CancellationTokenSource();
        _runningMacros[trigger] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < macro.RepeatCount && !cts.IsCancellationRequested; i++)
                    await MacroPlayer.PlayAsync(macro, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _runningMacros.Remove(trigger, out _);
            }
        });
    }

    private void StartLoop(Trigger trigger, Macro macro)
    {
        var cts = new CancellationTokenSource();
        _runningMacros[trigger] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                    await MacroPlayer.PlayAsync(macro, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    private static void Fire(BindingAction action)
    {
        switch (action)
        {
            case TextAction textAction:
                _ = Task.Run(() => InputSender.SendText(textAction.Text));
                break;

            case LaunchAppAction launchAction:
                _ = Task.Run(() => Process.Start(new ProcessStartInfo(launchAction.Path)
                {
                    Arguments = launchAction.Arguments ?? string.Empty,
                    UseShellExecute = true,
                }));
                break;
        }
    }
}
