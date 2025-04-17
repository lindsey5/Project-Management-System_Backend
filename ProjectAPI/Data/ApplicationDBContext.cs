using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;
using Task = ProjectAPI.Models.Task;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Assignee>()
            .HasOne(a => a.Member)
            .WithMany() 
            .HasForeignKey(a => a.Member_Id);

        modelBuilder.Entity<Assignee>()
            .HasOne(a => a.Task)
            .WithMany(t => t.Assignees)
            .HasForeignKey(a => a.Task_Id);
    }

}