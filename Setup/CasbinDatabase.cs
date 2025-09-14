using Casbin.Persist.Adapter.EFCore;
using Microsoft.EntityFrameworkCore;

public class CasbinDatabase : CasbinDbContext<Guid>
{
    public CasbinDatabase()
        : base(
            new DbContextOptionsBuilder<CasbinDbContext<Guid>>()
                .UseNpgsql(
                    "Host=localhost;Port=54321;Database=domain;Username=root;Password=root"
                )
                .Options,
            "casbin"
        ) { }

    public CasbinDatabase(DbContextOptions<CasbinDbContext<Guid>> options)
        : base(options, "casbin") { }
}
