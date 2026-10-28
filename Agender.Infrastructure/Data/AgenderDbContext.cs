using Agender.Infrastructure.Google;
using Agender.Infrastructure.Identity;
using Agender.Infrastructure.Realtime;
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

    private bool _written;

    public string UserId { get; set; } = UnclaimedUserId;

    public string ActorId { get; set; } = string.Empty;

    public IChangePublisher? ChangePublisher { get; set; }

    public void MarkWritten() => _written = true;

    public override void Dispose()
    {
        PublishChange();
        base.Dispose();
    }

    public override ValueTask DisposeAsync()
    {
        PublishChange();
        return base.DisposeAsync();
    }

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

    public DbSet<Household> Households => Set<Household>();

    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    public DbSet<HouseholdInvite> HouseholdInvites => Set<HouseholdInvite>();

    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();

    public DbSet<ShoppingItem> ShoppingItems => Set<ShoppingItem>();

    public DbSet<ListShare> ListShares => Set<ListShare>();

    public DbSet<HomeProject> HomeProjects => Set<HomeProject>();

    public DbSet<HomeTask> HomeTasks => Set<HomeTask>();

    public DbSet<WeddingSettings> WeddingDetails => Set<WeddingSettings>();

    public DbSet<WeddingGuest> WeddingGuests => Set<WeddingGuest>();

    public DbSet<WeddingCategory> WeddingCategories => Set<WeddingCategory>();

    public DbSet<WeddingPayment> WeddingPayments => Set<WeddingPayment>();

    public DbSet<WeddingVendor> WeddingVendors => Set<WeddingVendor>();

    public DbSet<WeddingTask> WeddingTasks => Set<WeddingTask>();

    public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();

    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();

    public DbSet<FinancePerson> FinancePeople => Set<FinancePerson>();

    public DbSet<SavingsAccount> SavingsAccounts => Set<SavingsAccount>();

    public DbSet<SavingsSnapshot> SavingsSnapshots => Set<SavingsSnapshot>();

    public DbSet<FamilyEvent> FamilyEvents => Set<FamilyEvent>();

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

        ConfigureHousehold(modelBuilder);

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

    private static void ConfigureHousehold(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Household>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Name).HasMaxLength(Household.NameMaxLength);
            entity.Property(h => h.InviteCode).HasMaxLength(Household.InviteCodeLength);
            entity.HasIndex(h => h.InviteCode).IsUnique();
        });

        modelBuilder.Entity<HouseholdMember>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.UserId).IsRequired().HasMaxLength(450);
            entity.Property(m => m.Color).HasMaxLength(ColorHex.Length);
            entity.HasIndex(m => m.UserId).IsUnique();
            entity.HasIndex(m => m.HouseholdId);
        });

        modelBuilder.Entity<HouseholdInvite>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Email).IsRequired().HasMaxLength(HouseholdInvite.EmailMaxLength);
            entity.Property(i => i.InvitedBy).HasMaxLength(450);
            entity.HasIndex(i => new { i.HouseholdId, i.Email }).IsUnique();
            entity.HasIndex(i => i.Email);
        });

        modelBuilder.Entity<ShoppingList>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Name).HasMaxLength(ShoppingList.NameMaxLength);
            entity.Property(l => l.Emoji).HasMaxLength(16);
            entity.Property(l => l.Color).HasMaxLength(ColorHex.Length);
            entity.Ignore(l => l.IsSharedWithMe);
            entity.Ignore(l => l.CanEdit);
            entity.Ignore(l => l.OwnerHouseholdName);
            entity.Ignore(l => l.TotalCount);
            entity.Ignore(l => l.PendingCount);
            entity.HasIndex(l => l.HouseholdId);
            entity.HasMany(l => l.Items).WithOne().HasForeignKey(i => i.ListId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(l => l.Shares).WithOne().HasForeignKey(s => s.ListId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Name).HasMaxLength(ShoppingItem.NameMaxLength);
            entity.Property(i => i.Unit).HasMaxLength(10);
            entity.Property(i => i.Category).HasMaxLength(40);
            entity.Property(i => i.Note).HasMaxLength(ShoppingItem.NoteMaxLength);
            entity.Property(i => i.AddedBy).HasMaxLength(AgenderUser.DisplayNameMaxLength);
        });

        modelBuilder.Entity<ListShare>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(s => new { s.ListId, s.Email }).IsUnique();
            entity.HasIndex(s => s.Email);
        });

        modelBuilder.Entity<HomeProject>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title).HasMaxLength(HomeProject.TitleMaxLength);
            entity.Property(p => p.Description).HasMaxLength(HomeProject.DescriptionMaxLength);
            entity.Property(p => p.Link).HasMaxLength(HomeProject.LinkMaxLength);
            entity.Property(p => p.Room).HasMaxLength(40);
            entity.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(p => p.HouseholdId);
            entity.HasMany(p => p.Tasks).WithOne().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HomeTask>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).HasMaxLength(HomeProject.TitleMaxLength);
        });

        modelBuilder.Entity<WeddingSettings>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Venue).HasMaxLength(WeddingSettings.VenueMaxLength);
            entity.HasIndex(w => w.HouseholdId).IsUnique();
        });

        modelBuilder.Entity<WeddingGuest>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Name).HasMaxLength(WeddingGuest.NameMaxLength);
            entity.Property(g => g.Group).HasMaxLength(WeddingGuest.TextMaxLength);
            entity.Property(g => g.Contact).HasMaxLength(WeddingGuest.TextMaxLength);
            entity.Property(g => g.Table).HasMaxLength(WeddingGuest.TextMaxLength);
            entity.Property(g => g.Diet).HasMaxLength(WeddingGuest.TextMaxLength);
            entity.Property(g => g.Notes).HasMaxLength(WeddingGuest.NotesMaxLength);
            entity.Property(g => g.Status).HasConversion<string>().HasMaxLength(20);
            entity.Ignore(g => g.People);
            entity.HasIndex(g => g.HouseholdId);
        });

        modelBuilder.Entity<WeddingCategory>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(WeddingCategory.NameMaxLength);
            entity.Ignore(c => c.Paid);
            entity.Ignore(c => c.Remaining);
            entity.HasIndex(c => c.HouseholdId);
            entity.HasMany(c => c.Payments).WithOne().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WeddingPayment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.PaidBy).HasMaxLength(WeddingPayment.PaidByMaxLength);
            entity.Property(p => p.Notes).HasMaxLength(WeddingPayment.NotesMaxLength);
            entity.HasIndex(p => p.HouseholdId);
        });

        modelBuilder.Entity<WeddingVendor>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Name).HasMaxLength(WeddingVendor.NameMaxLength);
            entity.Property(v => v.Kind).HasMaxLength(WeddingVendor.TextMaxLength);
            entity.Property(v => v.Contact).HasMaxLength(WeddingVendor.TextMaxLength);
            entity.Property(v => v.Notes).HasMaxLength(WeddingVendor.NotesMaxLength);
            entity.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(v => v.HouseholdId);
        });

        modelBuilder.Entity<WeddingTask>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).HasMaxLength(WeddingTask.TitleMaxLength);
            entity.Property(t => t.AssigneeUserId).HasMaxLength(450);
            entity.HasIndex(t => t.HouseholdId);
        });

        modelBuilder.Entity<FinanceTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Category).HasMaxLength(60);
            entity.Property(t => t.Description).HasMaxLength(FinanceTransaction.DescriptionMaxLength);
            entity.Ignore(t => t.SignedValue);
            entity.HasIndex(t => new { t.HouseholdId, t.Date });
        });

        modelBuilder.Entity<FinancePerson>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(FinancePerson.NameMaxLength);
            entity.Property(p => p.Color).HasMaxLength(ColorHex.Length);
            entity.Property(p => p.MemberUserId).HasMaxLength(450);
            entity.HasIndex(p => p.HouseholdId);
        });

        modelBuilder.Entity<SavingsAccount>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).HasMaxLength(SavingsAccount.NameMaxLength);
            entity.Property(a => a.Bank).HasMaxLength(SavingsAccount.BankMaxLength);
            entity.HasIndex(a => a.HouseholdId);
        });

        modelBuilder.Entity<SavingsSnapshot>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.HouseholdId, s.Year, s.PersonId }).IsUnique();
        });

        modelBuilder.Entity<SavingsGoal>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Name).HasMaxLength(SavingsGoal.NameMaxLength);
            entity.Property(g => g.Emoji).HasMaxLength(16);
            entity.Property(g => g.Color).HasMaxLength(ColorHex.Length);
            entity.Ignore(g => g.Progress);
            entity.HasIndex(g => g.HouseholdId);
        });

        modelBuilder.Entity<FamilyEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(FamilyEvent.TitleMaxLength);
            entity.Property(e => e.Location).HasMaxLength(FamilyEvent.LocationMaxLength);
            entity.Property(e => e.Notes).HasMaxLength(FamilyEvent.NotesMaxLength);
            entity.Property(e => e.MemberId).HasMaxLength(450);
            entity.Property(e => e.MemberName).HasMaxLength(AgenderUser.DisplayNameMaxLength);
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Recurrence).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.HouseholdId);
        });
    }

    private void PublishChange()
    {
        if (_written && ChangePublisher is not null && ActorId.Length > 0 && UserId.Length > 0)
        {
            ChangePublisher.Publish(UserId, ActorId);
        }

        _written = false;
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
