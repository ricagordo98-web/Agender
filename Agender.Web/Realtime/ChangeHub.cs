using Agender.Infrastructure.Realtime;
using Agender.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Agender.Web.Realtime;

[Authorize]
public sealed class ChangeHub : Hub
{
    public const string Path = "/hubs/changes";
    public const string Method = "changed";
}

public sealed class ChangeForwarder(ChangeBus bus, IHubContext<ChangeHub> hub) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        bus.Received += ForwardAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        bus.Received -= ForwardAsync;
        return Task.CompletedTask;
    }

    private Task ForwardAsync(ChangeNotice notice, IReadOnlyCollection<string> recipients) =>
        hub.Clients.Users(recipients).SendAsync(ChangeHub.Method, notice);
}
