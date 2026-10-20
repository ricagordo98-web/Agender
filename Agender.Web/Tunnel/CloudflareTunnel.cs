using System.Diagnostics;
using System.Text.RegularExpressions;
using Agender.Infrastructure.Sharing;
using Agender.Web.Auth;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace Agender.Web.Tunnel;

public sealed partial class CloudflareTunnel(IOptions<AgenderOptions> options, IServer server, ILogger<CloudflareTunnel> logger) : IPublicUrlProvider, IDisposable
{
    private const string ExecutableName = "cloudflared";
    private const string DownloadUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";
    private const string DnsQueryUrl = "https://1.1.1.1/dns-query";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private string? _url;
    private DateTime _stopAt;
    private CancellationTokenSource? _expiry;

    public async Task<string?> AcquireAsync(DateTime untilUtc, CancellationToken cancellationToken = default)
    {
        if (!options.Value.EnableTunnel)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_process is null || _process.HasExited)
            {
                _url = await StartAsync(cancellationToken);
            }

            ScheduleStop(untilUtc);
            return _url;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PrepareAsync()
    {
        if (!options.Value.EnableTunnel)
        {
            return;
        }

        await _gate.WaitAsync();

        try
        {
            await ResolveExecutableAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or TaskCanceledException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The tunnel program could not be prepared");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReleaseAsync()
    {
        await _gate.WaitAsync();

        try
        {
            Stop();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        Stop();
        _gate.Dispose();
    }

    [GeneratedRegex(@"https://[a-z0-9-]+\.trycloudflare\.com")]
    private static partial Regex UrlPattern();

    private async Task<string> StartAsync(CancellationToken cancellationToken)
    {
        string executable = await ResolveExecutableAsync(cancellationToken);

        ProcessStartInfo startInfo = new(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("tunnel");
        startInfo.ArgumentList.Add("--url");
        startInfo.ArgumentList.Add(LocalAddress());
        startInfo.ArgumentList.Add("--no-autoupdate");

        Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("The tunnel process could not be started.");
        TaskCompletionSource<string> found = new(TaskCreationOptions.RunContinuationsAsynchronously);

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null && UrlPattern().Match(e.Data) is { Success: true } match)
            {
                found.TrySetResult(match.Value);
            }
        };
        process.OutputDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StartTimeout);

        try
        {
            string url = await found.Task.WaitAsync(timeout.Token);
            await WaitUntilPublishedAsync(url, timeout.Token);
            _process = process;
            logger.LogInformation("Public tunnel started at {Url}", url);
            return url;
        }
        catch
        {
            Kill(process);
            throw;
        }
    }

    private static async Task WaitUntilPublishedAsync(string url, CancellationToken cancellationToken)
    {
        string host = new Uri(url).Host;

        while (true)
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, $"{DnsQueryUrl}?name={host}&type=A");
                request.Headers.Accept.ParseAdd("application/dns-json");
                using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken);
                string body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (body.Contains("\"Answer\"", StringComparison.Ordinal))
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);
        }
    }

    private string LocalAddress()
    {
        string address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault() ?? "http://localhost:5000";

        return address
            .Replace("://*", "://localhost", StringComparison.Ordinal)
            .Replace("://+", "://localhost", StringComparison.Ordinal)
            .Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal)
            .Replace("://[::]", "://localhost", StringComparison.Ordinal);
    }

    private static async Task<string> ResolveExecutableAsync(CancellationToken cancellationToken)
    {
        string fileName = OperatingSystem.IsWindows() ? ExecutableName + ".exe" : ExecutableName;
        string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Agender");
        string installed = Path.Combine(dataDirectory, fileName);

        if (File.Exists(installed))
        {
            return installed;
        }

        string? onPath = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, fileName))
            .FirstOrDefault(File.Exists);

        if (onPath is not null)
        {
            return onPath;
        }

        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string? downloaded = Directory.Exists(downloads) ? Directory.EnumerateFiles(downloads, "cloudflared*.exe").FirstOrDefault() : null;

        if (downloaded is not null && OperatingSystem.IsWindows())
        {
            return downloaded;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new FileNotFoundException("cloudflared was not found on the PATH.");
        }

        Directory.CreateDirectory(dataDirectory);
        string temporary = installed + ".download";

        using HttpClient client = new() { Timeout = DownloadTimeout };
        await using (Stream source = await client.GetStreamAsync(DownloadUrl, cancellationToken))
        await using (FileStream target = File.Create(temporary))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        File.Move(temporary, installed, overwrite: true);
        return installed;
    }

    private void ScheduleStop(DateTime untilUtc)
    {
        _stopAt = untilUtc > _stopAt ? untilUtc : _stopAt;
        _expiry?.Cancel();
        _expiry?.Dispose();
        _expiry = new CancellationTokenSource();

        _ = StopAtAsync(_stopAt - DateTime.UtcNow, _expiry.Token);
    }

    private async Task StopAtAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await ReleaseAsync();
    }

    private void Stop()
    {
        _expiry?.Cancel();
        _stopAt = default;

        if (_process is not null)
        {
            Kill(_process);
            _process = null;
            _url = null;
            logger.LogInformation("Public tunnel stopped");
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }

        process.Dispose();
    }
}
