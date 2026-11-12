namespace Agender.Shared.Abstractions;

public interface IDeviceNotifications
{
    bool IsEnabled { get; }

    TimeOnly Time { get; }

    Task<bool> EnableAsync(TimeOnly time, CancellationToken cancellationToken = default);

    void Disable();

    Task<bool> ShowNowAsync(CancellationToken cancellationToken = default);
}
