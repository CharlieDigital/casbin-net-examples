using Casbin;
using Casbin.Model;
using Casbin.Persist.Adapter.EFCore;

// dotnet run --treenode-filter "/*/*/CasbinBasicTests/*"
public class CasbinBasicTests
{
    // 👇 Here we inject the fixture to get a handle to get the context factory.
    [ClassDataSource<PgDatabaseFixture>(Shared = SharedType.PerTestSession)]
    public required PgDatabaseFixture Pg { get; init; }

    // Basic test to ensure the database is working correctly.
    [Test]
    public async Task Can_Perform_Basic_Enforcement()
    {
        // Set up both the application context and the Casbin context.
        await using var context = Pg.Factory.CreateDbContext();
        await using var casbinContext = Pg.CreateCasbinContext();
        await using var _ = await context.Database.BeginTransactionAsync();

        // Create the enforcer instance
        var enforcer = CreateEnforcer(casbinContext);

        // Set up the entity
        var alice = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion"
        };

        var aliceEntity = context.Users.Add(alice);
        await context.SaveChangesAsync();

        var aliceId = aliceEntity.Entity.Id.ToString();

        // Add a policy to the alice record
        enforcer.AddPolicy(aliceId, aliceId, "read", "Motion");

        await enforcer.SavePolicyAsync();

        // Check if alice can read her own record
        var canRead = await enforcer.EnforceAsync(
            aliceId,
            aliceId,
            "read",
            "Motion"
        );
        await Assert.That(canRead).IsTrue();

        // Check if alice can read her own record if a different domain
        var canReadDifferentDomain = await enforcer.EnforceAsync(
            aliceId,
            aliceId,
            "read",
            "Other_Company"
        );
        await Assert.That(canReadDifferentDomain).IsFalse();

        // Check if alice can write her own record (should be false)
        var canWrite = await enforcer.EnforceAsync(
            aliceId,
            aliceId,
            "write",
            "Motion"
        );
        await Assert.That(canWrite).IsFalse();

        // Check if alice can read a different record (should be false)
        var canReadOther = await enforcer.EnforceAsync(
            aliceId,
            Random.Shared.Next(10000, 999999).ToString(),
            "read",
            "Motion"
        );
        await Assert.That(canReadOther).IsFalse();

        // Check if bob can read alice's record (should be false)
        var canBobReadAlice = await enforcer.EnforceAsync(
            "bob_id",
            aliceId,
            "read",
            "Motion"
        );
        await Assert.That(canBobReadAlice).IsFalse();
    }

    /// <summary>
    /// Internal utility function to create an enforcer.
    /// </summary>
    private static Enforcer CreateEnforcer(CasbinDbContext<int> casbinContext)
    {
        var adapter = new EFCoreAdapter<int>(casbinContext);
        var enforcer = new Enforcer(CreateModel(), adapter);
        return enforcer;
    }

    /// <summary>
    /// Define the model here for these test cases.
    /// </summary>
    private static IModel CreateModel()
    {
        return DefaultModel.CreateFromText(
            """
            [request_definition]
            r = sub, obj, act, dom   # dom = domain or team

            [policy_definition]
            p = sub, obj, act, dom

            [role_definition]
            g = _, _, _       # g(user, role, domain)
            g2 = _, _         # g2(resource, parent) (resource hierarchy, domain optional if desired)

            [policy_effect]
            e = some(where (p.eft == allow))

            [matchers]
            m = (r.sub == p.sub && r.obj == p.obj && r.act == p.act && r.dom == p.dom)
                || (g(r.sub, p.sub, r.dom) && (r.obj == p.obj || g2(r.obj, p.obj)) && r.act == p.act)
            """
        );
    }
}
