using System.Collections.Generic;

namespace StackFlow.Models
{
    public class Role
    {
        public int Id { get; set; } // Primary key
        public string Title { get; set; } // This maps to the 'Title' column (not 'Name')
        public string Description { get; set; }

        public ICollection<User> Users { get; set; }

        public Role()
        {
            Users = new HashSet<User>();
        }
    }
}
