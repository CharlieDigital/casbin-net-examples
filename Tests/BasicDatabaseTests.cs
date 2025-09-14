using System.Threading.Tasks;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

// dotnet run --treenode-filter "/*/*/BasicDatabaseTests/*"
public class BasicDatabaseTests
{
    // In TUnit, the class is created once per test run, so we can use
    // instance fields to hold state
    private Database _db = null!;
    private IDbContextTransaction _transaction = null!;

    [Before(Test)]
    public async Task SetupAsync()
    {
        _db = Pg.Factory.CreateDbContext();
        _transaction = await _db.Database.BeginTransactionAsync();
    }

    [After(Test)]
    public async Task TeardownAsync()
    {
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        await _db.DisposeAsync();
    }

    // 👇 Here we inject the fixture to get a handle to get the context factory.
    [ClassDataSource<PgDatabaseFixture>(Shared = SharedType.PerTestSession)]
    public required PgDatabaseFixture Pg { get; init; }

    // Basic test to ensure the database is working correctly.
    [Test]
    public async Task Can_Use_Database()
    {
        var user = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var retrievedUser = await _db.Users.FirstAsync(u => u.Name == "Alice");

        await Assert.That(retrievedUser).IsNotNull();
        await Assert.That(retrievedUser.Email).IsEqualTo("alice@example.com");
    }

    [Test]
    public async Task Can_Access_User_With_Roles()
    {
        var user = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion",
            OrgRoles = [new() { Name = "Admin" }],
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var retrievedUser = await _db
            .Users.Include(u => u.OrgRoles)
            .FirstAsync(u => u.Name == "Alice");

        await Assert.That(retrievedUser).IsNotNull();
        await Assert.That(retrievedUser.Email).IsEqualTo("alice@example.com");
        await Assert.That(retrievedUser.OrgRoles).IsNotEmpty();
        await Assert.That(retrievedUser.OrgRoles[0].Name).IsEqualTo("Admin");
    }

    [Test]
    public async Task Can_Access_User_With_Workspaces()
    {
        var user = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion",
            OrgRoles = [new() { Name = "Admin" }],
            Workspaces = [new() { Name = "Workspace1" }]
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var retrievedUser = await _db
            .Users.Include(u => u.OrgRoles)
            .Include(u => u.Workspaces)
            .FirstAsync(u => u.Name == "Alice");

        await Assert.That(retrievedUser).IsNotNull();
        await Assert.That(retrievedUser.Email).IsEqualTo("alice@example.com");
        await Assert.That(retrievedUser.OrgRoles).IsNotEmpty();
        await Assert.That(retrievedUser.OrgRoles[0].Name).IsEqualTo("Admin");
        await Assert.That(retrievedUser.Workspaces).IsNotEmpty();
        await Assert.That(retrievedUser.Workspaces[0].Name).IsEqualTo("Workspace1");
    }
}
