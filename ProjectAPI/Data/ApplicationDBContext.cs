using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;

public class ApplicationDBContext : DbContext
{
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<ProjectAPI.Models.Task> Tasks { get; set; } = null!;
    public DbSet<Assignee> Assignees { get; set; } = null!;
    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<Request> Requests { get; set; } = null!;

}