using Casbin.Persist.Adapter.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

/// <summary>
/// This fixture sets up the database container and the DbContextFactory for tests
/// to use.
/// </summary>
public class PgDatabaseFixture : IAsyncInitializer, IAsyncDisposable
{
    private PostgreSqlContainer? _pg = null;
    private PostgreSqlContainer? _pgCasbin = null;

    public PooledDbContextFactory<Database> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Set up the container with reuse
        _pg = new PostgreSqlBuilder()
            .WithDatabase("domain")
            .WithReuse(true)
            .WithLabel("reuse-id", "test-casbin-domain")
            .Build();

        await _pg.StartAsync();

        // The base factory to create DbContext instances for our app.
        Factory = new PooledDbContextFactory<Database>(
            new DbContextOptionsBuilder<Database>()
                .UseNpgsql(_pg.GetConnectionString())
                .Options
        );

        // Delete the database and recreate it to make life easier.
        using var context = Factory.CreateDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await context.Database.MigrateAsync();

        // Now initialize the Casbin database.
        _pgCasbin = new PostgreSqlBuilder()
            .WithDatabase("casbin")
            .WithReuse(true)
            .WithLabel("reuse-id", "test-casbin")
            .Build();

        await _pgCasbin.StartAsync();

        await using var casbinContext = CreateCasbinContext();
        await casbinContext.Database.EnsureDeletedAsync();
        await casbinContext.Database.EnsureCreatedAsync();
        await casbinContext.Database.MigrateAsync();

        Console.WriteLine("✅  Initialized PgDatabaseFixture");
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("✅  Disposing PgDatabaseFixture");

        // Clean everything up at the end (does not delete the container so we can reuse it)
        if (_pg != null)
        {
            await _pg.StopAsync();
            await _pg.DisposeAsync();
        }

        if (_pgCasbin != null)
        {
            await _pgCasbin.StopAsync();
            await _pgCasbin.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    public CasbinDbContext<int> CreateCasbinContext()
    {
        var options = new DbContextOptionsBuilder<CasbinDbContext<int>>()
            .UseNpgsql(_pgCasbin.GetConnectionString())
            .Options;

        var casbinContext = new CasbinDbContext<int>(options);

        return casbinContext;
    }
}
