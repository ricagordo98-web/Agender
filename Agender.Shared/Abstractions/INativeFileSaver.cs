namespace Agender.Shared.Abstractions;

public interface INativeFileSaver
{
    bool CanCreatePdf { get; }

    Task SaveAsync(string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default);
}
