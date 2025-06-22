using StackFlow.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StackFlow.Models
{
    public class Role
    {
        [Key]
        [Column("id")]
        public int RoleId { get; set; } // Changed to RoleId for consistency with User.RoleId

        [Required]
        [Column("role_name")]
        [StringLength(255)] // Max length from schema
        public string Title { get; set; } // Changed to Title for clarity

        [Column("description", TypeName = "text")] // Using TypeName for 'text'
        public string Description { get; set; }

        public ICollection<User> Users { get; set; }
    }
}
