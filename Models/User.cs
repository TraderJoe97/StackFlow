using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

namespace StackFlow.Models
{
    public class User
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("username")]
        [StringLength(150)] // Max length from schema
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [Column("email")]
        [StringLength(255)] // Max length from schema
        public string Email { get; set; }

        [Required]
        [Column("password")]
        [StringLength(255)] // Max length for password hash
        public string PasswordHash { get; set; }

        [Column("created_at", TypeName = "date")] // Using TypeName for 'date'
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("role_id")] // Explicit foreign key property, good practice
        public int RoleId { get; set; }

        [ForeignKey("RoleId")] // Link to the navigation property
        public Role Role { get; set; }

        // Navigation property for Tasks created by this user
        public ICollection<Task> CreatedTasks { get; set; }

        // Navigation property for Tasks assigned to this user
        public ICollection<Task> AssignedTasks { get; set; }

        // Navigation property for TaskComments made by this user
        public ICollection<TaskComment> TaskComments { get; set; }

        // Navigation property for Projects created by this user
        public ICollection<Project> CreatedProjects { get; set; }
    }
}
