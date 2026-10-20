using Agender.Shared.Domain;

namespace Agender.Shared.Services;

public interface IUserSettingsService
{
    Task<UserSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default);
}
