using Microsoft.EntityFrameworkCore;
using StackFlow.Models;
using System.Linq; // Already there for LastOrDefault, keeping it.

namespace StackFlow.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<StackFlow.Models.Task> Tasks { get; set; } // No need for full namespace if 'Task' is not ambiguous
        public DbSet<TaskComment> TaskComments { get; set; } // Changed DbSet property name to TaskComments for consistency

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Explicitly map entity names to their snake_case table names in the database
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<Role>().ToTable("roles");
            modelBuilder.Entity<Project>().ToTable("projects");
            modelBuilder.Entity<StackFlow.Models.Task>().ToTable("tasks");
            modelBuilder.Entity<TaskComment>().ToTable("task_comments"); // Explicitly map TaskComment to task_comments table


            // User and Role relationship
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .IsRequired(); // RoleId is NOT NULL based on schema for user 'role id'

            // Project and User (CreatedBy) relationship
            modelBuilder.Entity<Project>()
                .HasOne(p => p.CreatedBy)
                .WithMany(u => u.CreatedProjects) // Assuming you have ICollection<Project> CreatedProjects in User.cs
                .HasForeignKey(p => p.CreatedByUserId)
                .IsRequired(); // Assuming created_by is NOT NULL

            // Task and Project relationship
            modelBuilder.Entity<StackFlow.Models.Task>()
                .HasOne(t => t.Project)
                .WithMany(p => p.Tasks) // Assuming Project has ICollection<Task> Tasks
                .HasForeignKey(t => t.ProjectId)
                .IsRequired(); // Assuming project_id is NOT NULL

            // Task and User (AssignedTo) relationship
            modelBuilder.Entity<StackFlow.Models.Task>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTasks) // Assuming User has ICollection<Task> AssignedTasks
                .HasForeignKey(t => t.AssignedToUserId)
                .IsRequired(false); // Assigned_to is NULLABLE

            // Task and User (TaskCreatedBy) relationship
            modelBuilder.Entity<StackFlow.Models.Task>()
                .HasOne(t => t.TaskCreatedBy)
                .WithMany(u => u.CreatedTasks) // Assuming User has ICollection<Task> CreatedTasks
                .HasForeignKey(t => t.TaskCreatedByUserId)
                .IsRequired(); // Assuming task_created_by is NOT NULL

            // TaskComment and Task relationship
            modelBuilder.Entity<TaskComment>()
                .HasOne(tc => tc.Task)
                .WithMany(t => t.TaskComments)
                .HasForeignKey(tc => tc.TaskId)
                .IsRequired(); // Assuming task_id is NOT NULL

            // TaskComment and User relationship
            modelBuilder.Entity<TaskComment>()
                .HasOne(tc => tc.User)
                .WithMany(u => u.TaskComments)
                .HasForeignKey(tc => tc.UserId)
                .IsRequired(); // Assuming user_id is NOT NULL


            // Seed a default project for easy testing (optional)
            // Ensure that a user with Id = 1 exists in your database or this will fail.
            // This seeding will only run if you apply migrations after adding it.
            modelBuilder.Entity<Project>().HasData(
                new Project
                {
                    Id = 1,
                    ProjectName = "Default Project",
                    ProjectDescription = "A default project for initial tasks.",
                    CreatedByUserId = 1, // Make sure a user with ID 1 exists (e.g., your initial admin user)
                    ProjectStartDate = DateTime.UtcNow.Date,
                    ProjectStatus = "Active"
                }
            );
        }
    }
}
