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
        public int RoleId { get; set; }

        [Required]
        [Column("role_name")]
        public string Title { get; set; }

        [Column("description")]
        public string Description { get; set; }

        public ICollection<User> Users { get; set; }
    }
}
