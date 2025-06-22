using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StackFlow.Models
{
    public class Task
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("task_title")]
        [StringLength(255)] // Max length from schema (varchar 255)
        public string TaskTitle { get; set; }

        [Column("task_description", TypeName = "text")] // TypeName for 'text'
        public string TaskDescription { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; } // Foreign key to Project

        [ForeignKey("ProjectId")]
        public Project Project { get; set; }

        [Column("assigned_to")]
        public int? AssignedToUserId { get; set; } // Nullable foreign key for unassigned tasks

        [ForeignKey("AssignedToUserId")]
        public User AssignedTo { get; set; }

        [Required]
        [Column("task_status")]
        [StringLength(20)] // Max length from schema (varchar 20)
        public string TaskStatus { get; set; } // e.g., "To Do", "In Progress", "Completed"

        [Column("task_priority")]
        [StringLength(10)] // Max length from schema (varchar 10)
        public string TaskPriority { get; set; } // e.g., "High", "Medium", "Low"

        [Column("task_created_by")]
        public int TaskCreatedByUserId { get; set; } // Foreign key to User

        [ForeignKey("TaskCreatedByUserId")]
        public User TaskCreatedBy { get; set; }

        [Column("task_created_at", TypeName = "date")]
        public DateTime TaskCreatedAt { get; set; } = DateTime.UtcNow;

        [Column("task_due_date", TypeName = "date")]
        public DateTime? TaskDueDate { get; set; } // Nullable date

        [Column("task_completed_at", TypeName = "date")]
        public DateTime? TaskCompletedAt { get; set; } // Nullable date

        // Navigation property for TaskComments related to this task
        public ICollection<TaskComment> TaskComments { get; set; }
    }

}
