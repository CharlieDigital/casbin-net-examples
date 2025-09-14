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
    private PostgreSqlContainer _pg = null!;

    public PooledDbContextFactory<Database> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Set up the container with reuse
        _pg = new PostgreSqlBuilder()
            .WithDatabase("casbin_test")
            .WithPortBinding(54321)
            .WithUsername("root")
            .WithPassword("root")
            .WithReuse(true)
            .WithName("test-casbin")
            .WithLabel("reuse-id", "test-casbin")
            .Build();

        await _pg.StartAsync();

        // The base factory to create DbContext instances for our app.
        Factory = new PooledDbContextFactory<Database>(
            new DbContextOptionsBuilder<Database>()
                .UseNpgsql(_pg.GetConnectionString())
                .Options
        );

        // Delete the database and recreate it to make life easier.
        using (var context = Factory.CreateDbContext())
        {
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            // Don't need to apply these since it will get created automatically.
        }

        using (var context = CreateCasbinContext())
        {
            // This is coming from a different pre-built context so we need to
            // apply it here
            await context.Database.MigrateAsync();
        }

        Console.WriteLine("✅  Initialized PgDatabaseFixture");
    }

    public async ValueTask DisposeAsync()
    {
        File.WriteAllText("TestOutput_Dispose.txt", "Starting DisposeAsync");

        Console.WriteLine("✅  Disposing PgDatabaseFixture");

        // Clean everything up at the end (does not delete the container so we can reuse it)
        if (_pg != null)
        {
            await _pg.StopAsync();
            await _pg.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    // Gets reused in test cases.
    public CasbinDatabase CreateCasbinContext()
    {
        var options = new DbContextOptionsBuilder<CasbinDbContext<Guid>>()
            .UseNpgsql(_pg!.GetConnectionString())
            .Options;

        // Same database, but different schema.
        var casbinDb = new CasbinDatabase(options);

        return casbinDb;
    }
}
