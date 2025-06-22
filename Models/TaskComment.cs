using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StackFlow.Models
{
    public class TaskComment
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("task_id")]
        public int TaskId { get; set; } // Foreign key to Task

        [ForeignKey("TaskId")]
        public Task Task { get; set; }

        [Column("user_id")]
        public int UserId { get; set; } // Foreign key to User

        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        [Column("comment", TypeName = "text")] // TypeName for 'text'
        public string CommentText { get; set; } // Renamed to avoid conflict with method 'Comment'

        [Column("comment_created_at", TypeName = "date")]
        public DateTime CommentCreatedAt { get; set; } = DateTime.UtcNow;
    }
}
