using Casbin;
using Casbin.Model;
using Casbin.Persist.Adapter.EFCore;
using Microsoft.EntityFrameworkCore.Storage;

public enum UserActions
{
    Read,
    Write,
    Delete
}

/// <summary>
/// NOTE: TUnit creates class-per-test and this is NOT the same behavior as xUnit
/// or NUnit.  So this approach of using local variables is specific to TUnit.
/// </summary>
// dotnet run --treenode-filter "/*/*/CasbinBuilderTests/*"
public class CasbinBuilderTests
{
    private Database _db = null!;
    private CasbinDbContext<Guid> _casbinDb = null!;
    private Enforcer _enforcer = null!;
    private IDbContextTransaction _transaction = null!;
    private IDbContextTransaction _casbinTransaction = null!;
    private User _alice = null!;

    [Before(Test)]
    public async Task SetupAsync()
    {
        _db = Pg.Factory.CreateDbContext();
        _casbinDb = Pg.CasbinFactory.CreateDbContext();
        // Two transactions because they are in different scopes.
        _transaction = await _db.Database.BeginTransactionAsync();
        _casbinTransaction = await _casbinDb.Database.BeginTransactionAsync();

        var adapter = new EFCoreAdapter<Guid>(_casbinDb);
        _enforcer = new Enforcer(CreateModel(), adapter);

        // Set up the entity (not strictly necessary here; but we want this here
        // so we can test the bulk scenario in other cases)
        var alice = new User
        {
            Name = "Alice",
            Email = "alice@example.com",
            Company = "Motion"
        };

        var aliceEntity = _db.Users.Add(alice);
        await _db.SaveChangesAsync();

        _alice = aliceEntity.Entity; // The entity has an ID.

        // Add a policy to the alice record
        await _enforcer
            .ForSubject(alice, "Motion")
            .Grant(UserActions.Read, alice)
            .SaveAsync();
    }

    [After(Test)]
    public async Task TeardownAsync()
    {
        // Clean everything up after each test.
        await _transaction.DisposeAsync();
        await _casbinTransaction.DisposeAsync();
        await _db.DisposeAsync();
        await _casbinDb.DisposeAsync();
    }

    // 👇 Here we inject the fixture to get a handle to get the context factory.
    [ClassDataSource<PgDatabaseFixture>(Shared = SharedType.PerTestSession)]
    public required PgDatabaseFixture Pg { get; init; }

    // Basic test to ensure the database is working correctly.
    [Test]
    public async Task Alice_Can_Read_Herself_With_Builder()
    {
        // Check if alice can read her own record
        var canRead = await _enforcer
            .ForSubject(_alice, "Motion")
            .VerifyAsync(UserActions.Read, _alice);
        await Assert.That(canRead).IsTrue();
    }

    [Test]
    public async Task Alice_Cannot_Read_Herself_In_Different_Domain_With_Builder()
    {
        // Check if alice can read her own record if a different domain
        var canReadDifferentDomain = await _enforcer
            .ForSubject(_alice, "Other_Company")
            .VerifyAsync(UserActions.Read, _alice);
        await Assert.That(canReadDifferentDomain).IsFalse();
    }

    [Test]
    public async Task Alice_Cannot_Write_Herself_With_Builder()
    {
        // Check if alice can write her own record (should be false)
        var canWrite = await _enforcer
            .ForSubject(_alice, "Motion")
            .VerifyAsync(UserActions.Write, _alice);
        await Assert.That(canWrite).IsFalse();
    }

    [Test]
    public async Task Alice_Cannot_Read_Other_Records_With_Builder()
    {
        // Check if alice can read a different record (should be false)
        var canReadOther = await _enforcer
            .ForSubject(_alice, "Motion")
            .VerifyAsync(
                UserActions.Read,
                Random.Shared.Next(10000, 999999).ToString()
            );
        await Assert.That(canReadOther).IsFalse();
    }

    [Test]
    public async Task Bob_Cannot_Read_Alice_Record_With_Builder()
    {
        // Check if bob can read alice's record (should be false)
        var canBobReadAlice = await _enforcer
            .ForSubject("bob_user_id", "Motion")
            .VerifyAsync(UserActions.Read, _alice);
        await Assert.That(canBobReadAlice).IsFalse();
    }

    [Test]
    public async Task Can_Read_Alice_Policies()
    {
        var policies = _enforcer.GetFilteredPolicy(0, _alice.Id.ToString());
        await Assert.That(policies.Count).IsEqualTo(1);

        var (sub, obj, act, dom) = policies.First().ToArray() switch
        {
            [var s, var o, var a, var d] => (s, o, a, d),
            _
                => throw new InvalidOperationException(
                    "Policy does not have 4 elements"
                )
        };

        await Assert.That(sub).IsEqualTo(_alice.Id.ToString());
        await Assert.That(obj).IsEqualTo(_alice.Id.ToString());
        await Assert.That(act).IsEqualTo("Read");
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
