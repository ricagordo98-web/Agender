namespace Agender.Shared.Services;

public interface IDatabaseImportService
{
    bool IsAvailable { get; }

    Task ImportAsync(byte[] database, CancellationToken cancellationToken = default);
}
