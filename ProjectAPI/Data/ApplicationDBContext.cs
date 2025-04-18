using Microsoft.EntityFrameworkCore;
using ProjectAPI.Models;
using ProjectAPI.Models.Task_Attachment;
using Task = ProjectAPI.Models.Task;

public class ApplicationDBContext : DbContext
{
    public ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<Task> Tasks { get; set; } = null!;
    public DbSet<Assignee> Assignees { get; set; } = null!;
    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<Request> Requests { get; set; } = null!;

    public DbSet<Task_Attachment> Task_Attachments { get; set; } = null!;

}