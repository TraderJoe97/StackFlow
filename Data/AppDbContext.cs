using Microsoft.EntityFrameworkCore;
using StackFlow.Models;
using System.Linq; // Added for LastOrDefault in OnModelCreating if needed

namespace StackFlow.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
    
        public DbSet<Project> Projects { get; set; }
        public DbSet<StackFlow.Models.Task> Tasks { get; set; }
        public DbSet<TaskComment> TaskComments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            // User and Role relationship (already partially covered by annotations)
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId) // Explicitly set the foreign key property
                .IsRequired(); // RoleId is NOT NULL based on schema for user 'role id'

            // Project and User (CreatedBy) relationship
            modelBuilder.Entity<Project>()
                .HasOne(p => p.CreatedBy)
                .WithMany(u => u.CreatedProjects)
                .HasForeignKey(p => p.CreatedByUserId)
                .IsRequired(); // Assuming created_by is NOT NULL

            // Task and Project relationship
            modelBuilder.Entity < StackFlow.Models.Task>()
                .HasOne(t => t.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(t => t.ProjectId)
                .IsRequired(); // Assuming project_id is NOT NULL

            // Task and User (AssignedTo) relationship
            modelBuilder.Entity<StackFlow.Models.Task>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedToUserId)
                .IsRequired(false); // Assigned_to is NULLABLE

            // Task and User (TaskCreatedBy) relationship
            modelBuilder.Entity<StackFlow.Models.Task>()
                .HasOne(t => t.TaskCreatedBy)
                .WithMany(u => u.CreatedTasks)
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
