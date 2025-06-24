using System.Collections.Generic;
// Removed: using Microsoft.AspNetCore.Identity; // No longer inherits from IdentityRole<TKey>

namespace StackFlow.Models
{
    // Custom Role model - no longer derives from IdentityRole
    public class Role
    {
        public int Id { get; set; } // Primary key
        public string Title { get; set; } // e.g., "Admin", "Project Manager", "Developer"
        public string Description { get; set; }

        // Navigation property for users in this role (optional, but good for relationships)
        public ICollection<User> Users { get; set; }

        public Role()
        {
            Users = new HashSet<User>();
        }
    }
}
