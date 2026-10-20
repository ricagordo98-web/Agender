namespace Agender.Infrastructure.Identity;

public interface ICurrentUser
{
    Task<string> GetUserIdAsync(CancellationToken cancellationToken = default);
}
