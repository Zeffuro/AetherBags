using System;
using System.Threading;

namespace AetherBags.Helpers;

public sealed class Debouncer(int delayMs) : IDisposable
{
    private Timer? _timer;

    public void Run(Action action)
    {
        _timer?.Dispose();

        _timer = new Timer(_ =>
        {
            action();
        }, null, delayMs, Timeout.Infinite);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}