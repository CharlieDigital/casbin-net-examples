// dotnet run --treenode-filter "/*/*/EmptyTest/*"
public class EmptyTest
{
    [ClassDataSource<PgDatabaseFixture>(Shared = SharedType.PerTestSession)]
    public required PgDatabaseFixture Pg { get; init; }

    [Test]
    public void Empty_Test()
    {
        // This test is intentionally left empty.
        // It serves as a placeholder to ensure the test suite runs without errors.
    }
}
