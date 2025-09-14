using DotNet.Testcontainers.Builders;
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
    private PostgreSqlContainer _pgCasbin = null!;

    public PooledDbContextFactory<Database> Factory { get; private set; } = null!;
    public PooledDbContextFactory<CasbinDatabase> CasbinFactory
    {
        get;
        private set;
    } = null!;

    public async Task InitializeAsync()
    {
        // Set up the container with reuse
        _pg = new PostgreSqlBuilder()
            .WithDatabase("domain")
            .WithReuse(true)
            .WithName("test-casbin-domain")
            .Build();

        _pgCasbin = new PostgreSqlBuilder()
            .WithDatabase("casbin")
            .WithPortBinding(54321)
            .WithReuse(true)
            .WithName("test-casbin")
            .Build();

        await Task.WhenAll(_pg.StartAsync(), _pgCasbin.StartAsync());

        var setupDomain = async Task () =>
        {
            File.WriteAllText(
                $"_setup-domain-start-{DateTime.UtcNow.Ticks}.txt",
                DateTime.UtcNow.ToString("O")
            );

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
            // Don't need to apply these since it will get created automatically.
        };

        var setupCasbin = async Task () =>
        {
            // The base factory to create DbContext instances for our app.
            CasbinFactory = new PooledDbContextFactory<CasbinDatabase>(
                new DbContextOptionsBuilder<CasbinDatabase>()
                    .UseNpgsql(_pgCasbin.GetConnectionString())
                    .Options
            );

            // Delete the database and recreate it to make life easier.
            using var context = CasbinFactory.CreateDbContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            // Don't need to apply these since it will get created automatically.
        };

        await Task.WhenAll(setupDomain(), setupCasbin());

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
}
