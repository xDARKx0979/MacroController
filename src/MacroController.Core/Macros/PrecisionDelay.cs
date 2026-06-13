using System.Diagnostics;

namespace MacroController.Core.Macros;

/// <summary>
/// <c>Task.Delay</c> resolution is ~15ms, which is too coarse for macro playback
/// timing. This sleeps for the bulk of the delay via <c>Task.Delay</c>, then
/// spin-waits the last few milliseconds against a <see cref="Stopwatch"/>.
/// </summary>
internal static class PrecisionDelay
{
    private const int SpinThresholdMs = 15;

    public static async Task WaitAsync(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds <= 0)
            return;

        if (milliseconds > SpinThresholdMs)
        {
            await Task.Delay(milliseconds - SpinThresholdMs, cancellationToken);
            milliseconds = SpinThresholdMs;
        }

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed.TotalMilliseconds < milliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.SpinWait(50);
        }
    }
}
