using Agender.Shared.Abstractions;

namespace Agender.Auth;

public sealed class MauiAppActivation : IAppActivation
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(30);

    private DateTimeOffset _lastRaised = DateTimeOffset.UtcNow;

    public event Action? Activated;

    public void Raise()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (now - _lastRaised < MinimumInterval)
        {
            return;
        }

        _lastRaised = now;
        Activated?.Invoke();
    }
}
