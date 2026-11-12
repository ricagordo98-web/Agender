using Agender.Auth;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiDatabaseImportService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IDatabaseImportService
{
    public bool IsAvailable => DeviceInfo.Platform == DevicePlatform.WinUI;

    public Task ImportAsync(byte[] database, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "/api/database/import", database, cancellationToken, TimeSpan.FromMinutes(3));

    public async Task<string> ExportAsync(CancellationToken cancellationToken = default)
    {
        byte[] database = await GetBytesAsync("/api/database/export", cancellationToken, TimeSpan.FromMinutes(3));
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, $"agender-{DateTime.Now:yyyy-MM-dd-HHmm}.db");
        await File.WriteAllBytesAsync(path, database, cancellationToken);

        return path;
    }
}
