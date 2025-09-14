using System.Transactions;
using Casbin;
using Casbin.Model;
using Casbin.Persist.Adapter.EFCore;
using Microsoft.EntityFrameworkCore.Storage;

// dotnet run --treenode-filter "/*/*/CasbinBasicTests/*"
public class CasbinBasicTests
{
    private Database _db = null!;
    private CasbinDbContext<int> _casbinDb = null!;
    private Enforcer _enforcer = null!;
    private IDbContextTransaction _transaction = null!;
    private IDbContextTransaction _casbinTransaction = null!;
    private string _aliceId = string.Empty;

    [Before(HookType.Test)]
    public async Task SetupAsync()
    {
        _db = Pg.Factory.CreateDbContext();
        _casbinDb = Pg.CreateCasbinContext();
        // Two transactions because they are in different scopes.
        _transaction = await _db.Database.BeginTransactionAsync();
        _casbinTransaction = await _casbinDb.Database.BeginTransactionAsync();

        var adapter = new EFCoreAdapter<int>(_casbinDb);
        _enforcer = new Enforcer(CreateModel(), adapter);

        // Set up the entity
        var alice = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion"
        };

        var aliceEntity = _db.Users.Add(alice);
        await _db.SaveChangesAsync();

        _aliceId = aliceEntity.Entity.Id.ToString();

        // Add a policy to the alice record
        _enforcer.AddPolicy(_aliceId, _aliceId, "read", "Motion");

        await _enforcer.SavePolicyAsync();
    }

    [After(HookType.Test)]
    public async Task TeardownAsync()
    {
        // Clean everything up.
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

    // Basic test to ensure the database is working correctly.
    [Test]
    public async Task Alice_Can_Read_Herself()
    {
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
