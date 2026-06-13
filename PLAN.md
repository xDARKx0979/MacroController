# MacroController — Project Plan

## What this is
A universal macro/remap tool (like Razer Synapse / Logitech G HUB macro engine) that
works with **any** keyboard or mouse, not just vendor hardware. Vendor tools only
"require" their own peripherals because their driver intercepts extra buttons before
Windows sees them — for normal keys/buttons, a global low-level input hook sees
everything already, so no special drivers are needed.

## Explicitly OUT of scope (for now)
- No "G-Shift" / secondary-layer / alt-bind-while-held feature. Keep it simple:
  one binding per trigger per profile.

## Core feature set
1. **Global capture** — low-level keyboard & mouse hooks (Win32 `WH_KEYBOARD_LL`,
   `WH_MOUSE_LL`) to detect any key/button press, anywhere, from any device.
2. **Macro recording** — record a sequence of key/mouse events with real timing
   (down/up + delay_ms between events), edit afterward.
3. **Macro playback** — replay recorded sequences via `SendInput`, which is
   indistinguishable from real hardware input to games/apps.
4. **Bindings** — map any physical key/button → an action:
   - Single key / key combo (remap)
   - Macro sequence
   - Text injection
   - Launch program / open file / run shortcut
5. **Profiles** — named sets of bindings, auto-switched based on the foreground
   application (`GetForegroundWindow` → process name match). Manual profile
   switch also supported.
6. **Clean UI** — this is a priority. Minimal, modern, fast. Tray-resident app,
   quick profile switcher, simple macro editor (timeline/list view with
   per-step delay editing), drag-and-drop binding assignment.
7. **Performance** — must feel instant. Hook callback and playback path need to
   be lightweight (no GC pressure, no blocking calls on the hook thread).

## Tech stack decision
**C# / .NET 8 + WPF** (or WinUI 3 if we want a more modern look later).

Why, and why NOT AutoHotkey:
- AHK's interpreter adds latency and isn't great for a polished custom UI.
- C# gives direct Win32 interop for `SetWindowsHookEx` + `SendInput` with
  minimal overhead — this is exactly how Razer/Logitech-style tools are built
  internally.
- WPF lets us build a genuinely clean, custom-styled UI (no AHK GUI ugliness),
  and we can ship as a single self-contained exe.
- Tray app + background service can live in the same process (no need for a
  separate Windows service for v1).

## Proposed architecture / project structure
```
MacroController/
  MacroController.sln
  src/
    MacroController.Core/        # no UI deps — reusable engine
      Hooks/
        KeyboardHook.cs          # WH_KEYBOARD_LL wrapper
        MouseHook.cs             # WH_MOUSE_LL wrapper
      Input/
        InputSender.cs           # SendInput wrapper for playback
        InputEvent.cs            # struct: device, code, action, delayMs
      Macros/
        Macro.cs                 # ordered list of InputEvents
        MacroRecorder.cs
        MacroPlayer.cs
      Bindings/
        Binding.cs               # trigger -> action
        BindingManager.cs        # active profile's lookup table, hot path
      Profiles/
        Profile.cs               # name, app match rule(s), bindings
        ProfileManager.cs        # loads/saves, watches foreground app
      Storage/
        ProfileStore.cs          # JSON serialize/deserialize to %AppData%
    MacroController.App/         # WPF UI + tray
      App.xaml / App.xaml.cs
      Views/
        MainWindow / Shell
        ProfileEditorView
        MacroEditorView
        BindingPickerView
      ViewModels/                # MVVM
      TrayIconService.cs
  PLAN.md
```

## Data model sketch
```csharp
record InputEvent(InputDevice Device, int Code, ActionType Action, int DelayMs);
// ActionType: KeyDown, KeyUp, MouseDown, MouseUp, MouseMove, Wheel

class Macro {
    string Name;
    List<InputEvent> Steps;
}

class Binding {
    string TriggerKey;          // physical key/button that fires this
    BindingType Type;            // Remap | Macro | Text | LaunchApp
    object Payload;              // Macro, string, or launch target
}

class Profile {
    string Name;
    List<string> MatchProcessNames;   // e.g. ["valorant.exe"]
    Dictionary<string, Binding> Bindings;
}
```

## Build order (milestones)
1. **Core engine spike**: global key hook that logs key presses to console,
   plus a `SendInput` test that replays a simple keystroke. Validates hook +
   injection round trip works reliably and with low latency.
2. **Macro record/playback**: record a sequence with real delays, save as
   JSON, play it back on a hotkey.
3. **Binding manager**: trigger key -> action lookup, intercept the trigger
   key in the hook (suppress original input when remapped) and fire the
   action.
4. **Profiles**: data model + foreground-window watcher to auto-switch active
   binding set.
5. **WPF shell + tray**: basic window, tray icon, profile list.
6. **Macro editor UI**: record button, timeline list of steps with editable
   delays, reorder/delete steps.
7. **Binding editor UI**: pick a trigger key (press-to-capture), assign
   action type and payload.
8. **Polish pass**: clean visual design, animations/transitions kept minimal
   for performance, settings (start with Windows, run in tray).

## Key implementation notes for whoever picks this up
- Hook callbacks must return fast — do lookups against a prebuilt dictionary
  (`BindingManager`), never do file I/O or UI work on the hook thread.
- To "swallow" a remapped key (so the original keypress doesn't also go
  through), return a non-zero value from the `WH_KEYBOARD_LL` callback.
- `SendInput` calls for macro playback should run on a background thread with
  a high-resolution timer (`Stopwatch`) for accurate delay timing — avoid
  `Thread.Sleep` for sub-15ms delays if precision matters (consider
  `Task.Delay` + spin-wait hybrid, or `Multimedia Timer`).
- Profile auto-switching: poll `GetForegroundWindow` on a ~250-500ms timer is
  fine; no need for event hooks initially.
- Storage: simple JSON files per profile in `%AppData%\MacroController\profiles\`.
- This folder (`MacroControler`) is independent of any other project context —
  treat it as its own standalone repo. Init git here.

## Status
Planning complete. No code written yet. Next step: milestone 1 (core hook +
SendInput spike).
