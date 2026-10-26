using Agender.Auth;
using Agender.Shared.Abstractions;
using Agender.Shared.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace Agender.Api;

public sealed class HubChangeFeed : IChangeFeed
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    private readonly AuthSession _session;
    private readonly object _gate = new();
    private HubConnection? _connection;
    private string? _url;
    private CancellationTokenSource? _loop;

    public HubChangeFeed(AuthSession session)
    {
        _session = session;
        _session.Changed += OnSessionChanged;
    }

    public event Func<ChangeNotice, Task>? Changed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        string url = $"{_session.ServerUrl}/hubs/changes";

        lock (_gate)
        {
            if (!_session.IsAuthenticated || (_loop is not null && _url == url))
            {
                return Task.CompletedTask;
            }

            StopLocked();
            _url = url;
            _loop = new CancellationTokenSource();
            _ = ConnectLoopAsync(url, _loop.Token);
        }

        return Task.CompletedTask;
    }

    private void OnSessionChanged()
    {
        lock (_gate)
        {
            if (!_session.IsAuthenticated)
            {
                StopLocked();
            }
        }
    }

    private void StopLocked()
    {
        _loop?.Cancel();
        _loop = null;
        _url = null;

        HubConnection? connection = _connection;
        _connection = null;

        if (connection is not null)
        {
            _ = connection.DisposeAsync().AsTask();
        }
    }

    private async Task ConnectLoopAsync(string url, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HubConnection connection = new HubConnectionBuilder()
                .WithUrl(url, options => options.AccessTokenProvider = () => _session.GetAccessTokenAsync())
                .WithAutomaticReconnect()
                .Build();

            connection.On<ChangeNotice>("changed", RaiseAsync);

            try
            {
                await connection.StartAsync(cancellationToken);

                lock (_gate)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _ = connection.DisposeAsync().AsTask();
                        return;
                    }

                    _connection = connection;
                }

                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await connection.DisposeAsync();
            }

            try
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RaiseAsync(ChangeNotice notice)
    {
        Func<ChangeNotice, Task>? handlers = Changed;

        if (handlers is null)
        {
            return;
        }

        foreach (Func<ChangeNotice, Task> handler in handlers.GetInvocationList().Cast<Func<ChangeNotice, Task>>())
        {
            try
            {
                await handler(notice);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }
        }
    }
}
