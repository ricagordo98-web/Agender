using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Agender.Infrastructure.Data;

public class AgenderDbContextFactory : IDesignTimeDbContextFactory<AgenderDbContext>
{
    public AgenderDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<AgenderDbContext> options = new DbContextOptionsBuilder<AgenderDbContext>()
            .UseSqlite("Data Source=agender-design.db")
            .Options;

        return new AgenderDbContext(options);
    }
}
