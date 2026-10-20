namespace Agender.Infrastructure.Sharing;

public interface IPublicUrlProvider
{
    Task<string?> AcquireAsync(DateTime untilUtc, CancellationToken cancellationToken = default);

    Task ReleaseAsync();
}
