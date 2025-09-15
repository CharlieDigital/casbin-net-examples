using Casbin;
using Casbin.Model;
using Casbin.Persist;
using Casbin.Persist.Adapter.EFCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// NOTE: TUnit creates class-per-test and this is NOT the same behavior as xUnit
/// or NUnit.  So this approach of using local variables is specific to TUnit.
/// </summary>
// dotnet run --treenode-filter "/*/*/CasbinBasicTests/*"
public class CasbinBasicTests
{
    private Database _db = null!;
    private CasbinDbContext<Guid> _casbinDb = null!;
    private Enforcer _enforcer = null!;
    private IDbContextTransaction _transaction = null!;
    private IDbContextTransaction _casbinTransaction = null!;
    private string _aliceId = string.Empty;

    [Before(Test)]
    public async Task SetupAsync()
    {
        _db = Pg.Factory.CreateDbContext();
        _casbinDb = Pg.CasbinFactory.CreateDbContext();
        // Two transactions because they are in different scopes.
        _transaction = await _db.Database.BeginTransactionAsync();
        _casbinTransaction = await _casbinDb.Database.BeginTransactionAsync();

        _enforcer = CreateEnforcer("default");

        // Set up the entity (not strictly necessary here; but we want this here
        // so we can test the bulk scenario in other cases)
        var alice = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion",
        };

        var aliceEntity = _db.Users.Add(alice);
        Console.WriteLine("Adding Alice");
        await _db.SaveChangesAsync();

        _aliceId = aliceEntity.Entity.Id.ToString();

        Console.WriteLine("Adding Policy");
        // Add a policy to the alice record
        _enforcer.AddPolicy(_aliceId, _aliceId, "read", "Motion");

        await _enforcer.SavePolicyAsync();
        Console.WriteLine("Policy Saved");
    }

    private Enforcer CreateEnforcer(string name)
    {
        Console.WriteLine($"Creating Enforcer: {name}");
        var adapter = new EFCoreAdapter<Guid>(_casbinDb);
        var enforcer = new Enforcer(CreateModel(), adapter);
        Console.WriteLine($"Created Enforce: {name}");

        return enforcer;
    }

    [After(Test)]
    public async Task TeardownAsync()
    {
        // Clean everything up after each test.
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        await _casbinTransaction.RollbackAsync();
        await _casbinTransaction.DisposeAsync();
        await _db.DisposeAsync();
        await _casbinDb.DisposeAsync();
    }

    // 👇 Here we inject the fixture to get a handle to get the context factory.
    [ClassDataSource<PgDatabaseFixture>(Shared = SharedType.PerTestSession)]
    public required PgDatabaseFixture Pg { get; init; }

    [Test]
    public async Task Multiple_Enforcers()
    {
        var enforcer2 = CreateEnforcer("2");

        Console.WriteLine("Adding write policy to 'default'");
        _enforcer.AddPolicy(_aliceId, _aliceId, "write", "Motion");

        Console.WriteLine("Enforce on default");
        // Check if alice can read her own record
        var canRead = await _enforcer.EnforceAsync(
            _aliceId,
            _aliceId,
            "write",
            "Motion"
        );
        await Assert.That(canRead).IsTrue();

        Console.WriteLine("Enforce on 2");
        var canRead2 = await enforcer2.EnforceAsync(
            _aliceId,
            _aliceId,
            "write",
            "Motion"
        );
        // This is actually false because the policy wasn't loaded into the 2nd enforcer.
        // In order to propogate these policy changes, we need a Watcher.
        await Assert.That(canRead2).IsFalse();
    }

    // Basic test to ensure the database is working correctly.
    [Test]
    public async Task Alice_Can_Read_Herself()
    {
        Console.WriteLine("Enforce");
        // Check if alice can read her own record
        var canRead = await _enforcer.EnforceAsync(
            _aliceId,
            _aliceId,
            "read",
            "Motion"
        );
        await Assert.That(canRead).IsTrue();
    }

    [Test]
    public async Task Alice_Cannot_Read_Herself_In_Different_Domain()
    {
        // Check if alice can read her own record if a different domain
        var canReadDifferentDomain = await _enforcer.EnforceAsync(
            _aliceId,
            _aliceId,
            "read",
            "Other_Company"
        );
        await Assert.That(canReadDifferentDomain).IsFalse();
    }

    [Test]
    public async Task Alice_Cannot_Write_Herself()
    {
        // Check if alice can write her own record (should be false)
        var canWrite = await _enforcer.EnforceAsync(
            _aliceId,
            _aliceId,
            "write",
            "Motion"
        );
        await Assert.That(canWrite).IsFalse();
    }

    [Test]
    public async Task Alice_Cannot_Read_Other_Records()
    {
        // Check if alice can read a different record (should be false)
        var canReadOther = await _enforcer.EnforceAsync(
            _aliceId,
            Random.Shared.Next(10000, 999999).ToString(),
            "read",
            "Motion"
        );
        await Assert.That(canReadOther).IsFalse();
    }

    [Test]
    public async Task Bob_Cannot_Read_Alice_Record()
    {
        // Check if bob can read alice's record (should be false)
        var canBobReadAlice = await _enforcer.EnforceAsync(
            "bob_id",
            _aliceId,
            "read",
            "Motion"
        );
        await Assert.That(canBobReadAlice).IsFalse();
    }

    [Test]
    public async Task Can_Read_Alice_Policies()
    {
        var policies = _enforcer.GetFilteredPolicy(0, _aliceId);
        await Assert.That(policies.Count).IsEqualTo(1);

        var (sub, obj, act, dom) = policies.First().ToArray() switch
        {
            [var s, var o, var a, var d] => (s, o, a, d),
            _ => throw new InvalidOperationException(
                "Policy does not have 4 elements"
            ),
        };

        await Assert.That(sub).IsEqualTo(_aliceId);
        await Assert.That(obj).IsEqualTo(_aliceId);
        await Assert.That(act).IsEqualTo("read");
        await Assert.That(dom).IsEqualTo("Motion");
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
            g2 = _, _         # g2(resource, parent) (resource hierarchy)

            [policy_effect]
            e = some(where (p.eft == allow))

            [matchers]
            m = (r.sub == p.sub && r.obj == p.obj && r.act == p.act && r.dom == p.dom)
                || (g(r.sub, p.sub, r.dom) && (r.obj == p.obj || g2(r.obj, p.obj)) && r.act == p.act)
            """
        );
    }
}
