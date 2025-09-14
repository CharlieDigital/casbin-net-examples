using Microsoft.EntityFrameworkCore;

// dotnet run --treenode-filter "/*/*/BasicDatabaseTests/*"
public class BasicDatabaseTests
{
    // 👇 Here we inject the fixture to get a handle to get the context factory.
    [ClassDataSource<PgDatabaseFixture>(Shared = SharedType.PerTestSession)]
    public required PgDatabaseFixture Pg { get; init; }

    // Basic test to ensure the database is working correctly.
    [Test]
    public async Task Can_Use_Database()
    {
        await using var context = Pg.Factory.CreateDbContext();
        await using var _ = await context.Database.BeginTransactionAsync();

        var user = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrievedUser = await context.Users.FirstAsync(u => u.Name == "Alice");

        await Assert.That(retrievedUser).IsNotNull();
        await Assert.That(retrievedUser.Email).IsEqualTo("alice@example.com");
    }

    [Test]
    public async Task Can_Access_User_With_Roles()
    {
        await using var context = Pg.Factory.CreateDbContext();
        await using var _ = await context.Database.BeginTransactionAsync();

        var user = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion",
            OrgRoles = [new() { Name = "Admin" }],
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrievedUser = await context
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
        await using var context = Pg.Factory.CreateDbContext();
        await using var _ = await context.Database.BeginTransactionAsync();

        var user = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion",
            OrgRoles = [new() { Name = "Admin" }],
            Workspaces = [new() { Name = "Workspace1" }]
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrievedUser = await context
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
