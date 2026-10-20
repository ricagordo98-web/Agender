namespace Agender.Shared.Services;

public interface ITagService
{
    Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken = default);
}
