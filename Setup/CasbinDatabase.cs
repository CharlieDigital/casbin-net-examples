using Casbin.Persist.Adapter.EFCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Create this wrapper so we can create the migration
/// </summary>
public class CasbinDatabase(DbContextOptions options)
    : CasbinDbContext<Guid>(options, "casbin")
{
    public CasbinDatabase()
        : this(
            new DbContextOptionsBuilder<CasbinDbContext<Guid>>()
                .UseNpgsql(
                    "Host=localhost;Port=54321;Database=domain;Username=root;Password=root"
                )
                .Options
        ) { }
}
