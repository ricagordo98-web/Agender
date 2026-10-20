using Agender.Infrastructure.Google;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Sharing;
using Agender.Shared.Domain;
using Agender.Shared.Services;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Agender.Infrastructure.Data;

public class AgenderDbContext(DbContextOptions<AgenderDbContext> options) : IdentityDbContext<AgenderUser>(options)
{
    public const string UserIdProperty = "UserId";
    public const string UnclaimedUserId = "";

    public string UserId { get; set; } = UnclaimedUserId;

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<DayNote> DayNotes => Set<DayNote>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<ActiveTimer> ActiveTimers => Set<ActiveTimer>();

    public DbSet<GoogleConnection> GoogleConnections => Set<GoogleConnection>();

    public DbSet<Agenda> Agendas => Set<Agenda>();

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<AgendaShare> AgendaShares => Set<AgendaShare>();

    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgenderUser>().Property(u => u.DisplayName).HasMaxLength(AgenderUser.DisplayNameMaxLength);

        modelBuilder.Entity<TimeEntry>(entity =>
        {
            ConfigureOwnership(entity);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(TimeEntry.DescriptionMaxLength);
            entity.Property(e => e.Color).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.CustomColor).HasMaxLength(ColorHex.Length);
            entity.Property(e => e.GoogleEventId).HasMaxLength(GoogleConnection.CalendarIdMaxLength);
            entity.HasIndex(UserIdProperty, nameof(TimeEntry.Date));
            entity.HasOne<TaskItem>().WithMany().HasForeignKey(e => e.TaskItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Project>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.Tags).WithMany().UsingEntity(join => join.ToTable("EntryTags"));
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            ConfigureOwnership(entity);
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).HasMaxLength(TaskItem.TitleMaxLength);
            entity.Property(t => t.Color).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.CustomColor).HasMaxLength(ColorHex.Length);
            entity.Ignore(t => t.IsScheduled);
            entity.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            ConfigureOwnership(entity);
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(Project.NameMaxLength);
            entity.Property(p => p.ClientName).HasMaxLength(Project.ClientNameMaxLength);
            entity.Property(p => p.Color).HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.CustomColor).HasMaxLength(ColorHex.Length);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            ConfigureOwnership(entity);
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(Tag.NameMaxLength);
            entity.Property(t => t.NormalizedName).HasMaxLength(Tag.NameMaxLength);
            entity.HasIndex(UserIdProperty, nameof(Tag.NormalizedName)).IsUnique();
        });

        modelBuilder.Entity<ActiveTimer>(entity =>
        {
            ConfigureOwnership(entity);
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Description).HasMaxLength(TimeEntry.DescriptionMaxLength);
            entity.HasIndex(UserIdProperty).IsUnique();
            entity.HasOne<TaskItem>().WithMany().HasForeignKey(t => t.TaskItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GoogleConnection>(entity =>
        {
            ConfigureOwnership(entity);
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ProtectedCredentials).HasMaxLength(GoogleConnection.CredentialsMaxLength);
            entity.Property(c => c.ServiceAccountEmail).HasMaxLength(GoogleConnection.EmailMaxLength);
            entity.Property(c => c.CalendarId).HasMaxLength(GoogleConnection.CalendarIdMaxLength);
            entity.Property(c => c.WriteCalendarId).HasMaxLength(GoogleConnection.CalendarIdMaxLength);
            entity.Property(c => c.TimeZone).HasMaxLength(GoogleConnection.TimeZoneMaxLength);
            entity.HasIndex(UserIdProperty).IsUnique();
        });

        modelBuilder.Entity<Agenda>(entity =>
        {
            ConfigureOwnership(entity);
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).HasMaxLength(Agenda.NameMaxLength);
            entity.Property(a => a.Color).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.CustomColor).HasMaxLength(ColorHex.Length);
            entity.HasIndex(UserIdProperty).HasFilter("\"IsDefault\" = 1").IsUnique();
        });

        modelBuilder.Entity<UserSettings>(entity =>
        {
            ConfigureOwnership(entity);
            entity.HasKey(s => s.Id);
            entity.HasIndex(UserIdProperty).IsUnique();
        });

        modelBuilder.Entity<AgendaShare>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.OwnerId).IsRequired().HasMaxLength(450);
            entity.Property(s => s.ViewerId).IsRequired().HasMaxLength(450);
            entity.HasIndex(s => new { s.OwnerId, s.ViewerId }).IsUnique();
            entity.HasIndex(s => s.ViewerId);
        });

        modelBuilder.Entity<ShareLink>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.OwnerId).IsRequired().HasMaxLength(450);
            entity.Property(l => l.TokenHash).IsRequired().HasMaxLength(ShareLink.TokenHashLength);
            entity.HasIndex(l => l.TokenHash).IsUnique();
            entity.HasIndex(l => l.OwnerId).IsUnique();
        });

        modelBuilder.Entity<DayNote>(entity =>
        {
            ConfigureOwnership(entity);
            entity.Property(n => n.Text).HasMaxLength(DayNote.TextMaxLength);
            entity.HasKey(UserIdProperty, nameof(DayNote.Date));
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampOwner();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new PersistenceException("Could not save the changes.", ex);
        }
    }

    private void ConfigureOwnership<T>(EntityTypeBuilder<T> entity)
        where T : class
    {
        entity.Property<string>(UserIdProperty).IsRequired().HasMaxLength(450).HasDefaultValue(UnclaimedUserId);
        entity.HasQueryFilter(e => EF.Property<string>(e, UserIdProperty) == UserId);
    }

    private void StampOwner()
    {
        foreach (EntityEntry entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added && e.Metadata.FindProperty(UserIdProperty) is not null))
        {
            entry.Property(UserIdProperty).CurrentValue = UserId;
        }
    }
}
