using Agender.Shared.Abstractions;
using Microsoft.JSInterop;

namespace Agender.Web.Auth;

public sealed class BrowserClock(IJSRuntime js) : IClock
{
    private TimeSpan _offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
    private bool _initialized;

    public DateTime Now => DateTime.UtcNow + _offset;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await using IJSObjectReference module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./clock.js");
        int offsetMinutes = await module.InvokeAsync<int>("getUtcOffsetMinutes", cancellationToken);

        _offset = TimeSpan.FromMinutes(offsetMinutes);
        _initialized = true;
    }
}
