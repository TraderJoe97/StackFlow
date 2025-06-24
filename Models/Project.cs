using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StackFlow.Models
{
    public class Project
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("project_name")]
        [StringLength(255)] // Max length based on common naming conventions, schema says varchar 255 for username/email. Adjust if you have a specific length in mind for project name.
        public string ProjectName { get; set; }

        [Column("project_description", TypeName = "text")] // TypeName for 'text'
        public string ProjectDescription { get; set; }

        [Column("created_by")]
        public int CreatedByUserId { get; set; } // Foreign key to User

        [ForeignKey("CreatedByUserId")]
        public User CreatedBy { get; set; }

        [Column("project_start_date", TypeName = "date")]
        public DateTime? ProjectStartDate { get; set; } // Nullable date

        [Column("project_end_date", TypeName = "date")]
        public DateTime? ProjectEndDate { get; set; } // Nullable date

        [Column("project_status")]
        [StringLength(50)] // Max length based on schema (varchar 50 for task status)
        public string ProjectStatus { get; set; }

        // Navigation property for Tasks belonging to this project
        public ICollection<Ticket> Tickets { get; set; }
    }
}
