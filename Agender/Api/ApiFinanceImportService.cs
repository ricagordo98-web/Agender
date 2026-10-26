using Agender.Auth;
using Agender.Shared.Contracts;
using Agender.Shared.Services;

namespace Agender.Api;

public sealed class ApiFinanceImportService(IHttpClientFactory httpClientFactory, AuthSession session)
    : ApiClientBase(httpClientFactory, session), IFinanceImportService
{
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(3);

    public Task<ImportReport> ImportAsync(byte[] workbook, bool apply, CancellationToken cancellationToken = default) =>
        SendBytesAsync<ImportReport>(HttpMethod.Post, $"/api/casa/finance/import?apply={(apply ? "true" : "false")}", workbook, cancellationToken, UploadTimeout);
}
