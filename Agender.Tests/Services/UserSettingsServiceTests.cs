using Agender.Infrastructure.Services;
using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Services;

public sealed class UserSettingsServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAsync_NothingSaved_ReturnsDefaults()
    {
        UserSettings settings = await new UserSettingsService(_db).GetAsync();

        settings.DefaultWeekView.Should().BeTrue();
        settings.SnapMinutes.Should().Be(5);
        settings.DefaultEntryMinutes.Should().Be(30);
        settings.ConfirmBeforeDelete.Should().BeTrue();
        settings.RemoveFromAgendaWhenCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_TwiceThenGet_ReturnsTheLatestAndKeepsASingleRow()
    {
        UserSettingsService sut = new(_db);

        await sut.SaveAsync(new UserSettings { SnapMinutes = 15 });
        await sut.SaveAsync(new UserSettings { SnapMinutes = 1, RemoveFromAgendaWhenCompleted = true });

        UserSettings settings = await sut.GetAsync();
        settings.SnapMinutes.Should().Be(1);
        settings.RemoveFromAgendaWhenCompleted.Should().BeTrue();
        using Agender.Infrastructure.Data.AgenderDbContext context = _db.Create(TestDb.DefaultUserId);
        context.UserSettings.Count().Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_OtherUsersSettings_AreNotAffected()
    {
        await new UserSettingsService(_db.ForUser("user-a")).SaveAsync(new UserSettings { SnapMinutes = 15 });

        UserSettings other = await new UserSettingsService(_db.ForUser("user-b")).GetAsync();

        other.SnapMinutes.Should().Be(5);
    }

    [Fact]
    public async Task SaveAsync_InvalidSnap_Throws()
    {
        Func<Task> act = () => new UserSettingsService(_db).SaveAsync(new UserSettings { SnapMinutes = 7 });

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
