using Microsoft.EntityFrameworkCore;
using StackFlow.Models;

namespace StackFlow.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Ticket> Tickets { get; set; } // Changed from Tasks to Tickets
        public DbSet<TicketComment> TicketComments { get; set; } // Changed from TaskComments to TicketComments

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure User and Role relationship
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Project and User relationships
            modelBuilder.Entity<Project>()
                .HasOne(p => p.CreatedBy)
                .WithMany(u => u.CreatedProjects)
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Ticket relationships (formerly Task)
            modelBuilder.Entity<Ticket>() // Changed from Task to Ticket
                .HasOne(t => t.Project)
                .WithMany(p => p.Tickets) // Changed from p.Tasks to p.Tickets
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Ticket>() // Changed from Task to Ticket
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTickets) // Changed from u.AssignedTasks to u.AssignedTickets
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull); // When a user is deleted, set their assigned tickets' AssignedToUserId to NULL

            modelBuilder.Entity<Ticket>() // Changed from Task to Ticket
                .HasOne(t => t.CreatedBy) // Changed from t.TicketCreatedBy to t.CreatedBy
                .WithMany(u => u.CreatedTickets) // Changed from u.CreatedTasks to u.CreatedTickets
                .HasForeignKey(t => t.CreatedByUserId) // Changed from t.TicketCreatedByUserId to t.CreatedByUserId
                .OnDelete(DeleteBehavior.NoAction); // CHANGE: Set to NoAction to break potential cycles.
                                                    // Application code must handle tickets created by a deleted user.

            // Configure TicketComment relationships (formerly TaskComment)
            modelBuilder.Entity<TicketComment>() // Changed from TaskComment to TicketComment
                .HasOne(tc => tc.Ticket) // Changed from tc.Task to tc.Ticket
                .WithMany(t => t.Comments) // Changed from t.TicketComments to t.Comments
                .HasForeignKey(tc => tc.TicketId) // Changed from tc.TaskId to tc.TicketId
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketComment>() // Changed from TaskComment to TicketComment
                .HasOne(tc => tc.User)
                .WithMany(u => u.TicketComments) // Changed from u.TaskComments to u.TicketComments
                .HasForeignKey(tc => tc.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Example of seeding roles
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Title = "Admin", Description = "Administrator with full access" },
                new Role { Id = 2, Title = "Project Manager", Description = "Manages projects and tickets" },
                new Role { Id = 3, Title = "Developer", Description = "Works on assigned tickets" }
            );

            // Add CHECK constraint for TicketStatus
            modelBuilder.Entity<Ticket>()
                .ToTable(t => t.HasCheckConstraint("CK_Ticket_Status", "Status IN ('To Do', 'In Progress', 'In Review', 'Done')"))
                .Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Add CHECK constraint for ProjectStatus - CORRECTED TO SNAKE_CASE
            modelBuilder.Entity<Project>()
                .ToTable(t => t.HasCheckConstraint("CK_Project_Status", "project_status IN ('Active', 'Completed', 'On Hold')")) // CHANGED to project_status
                .Property(p => p.ProjectStatus)
                .HasConversion<string>()
                .HasMaxLength(20);

            // Add CHECK constraint for TicketPriority
            modelBuilder.Entity<Ticket>()
                .ToTable(t => t.HasCheckConstraint("CK_Ticket_Priority", "Priority IN ('Low', 'Medium', 'High')"))
                .Property(t => t.Priority)
                .HasConversion<string>()
                .HasMaxLength(10);
        }
    }
}
