using Microsoft.EntityFrameworkCore;

public class Database(DbContextOptions<Database> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
}

public class User : IEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Company { get; set; } = "Motion";
    public List<OrgRole> OrgRoles { get; set; } = [];
    public List<Workspace> Workspaces { get; set; } = [];
}

public class OrgRole
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public class Workspace : IEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public interface IEntity
{
    int Id { get; set; }
    string Name { get; set; }
}
