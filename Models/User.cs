using System;
using System.Collections.Generic;

namespace StackFlow.Models
{
    public class User
    {
        public int Id { get; set; } // Primary key
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign key for Role
        public int? RoleId { get; set; }

        // Navigation property for Role
        public Role Role { get; set; }

        // Navigation property for Tickets created by this user
        public ICollection<Ticket> CreatedTickets { get; set; } // Renamed from CreatedTasks

        // Navigation property for Tickets assigned to this user
        public ICollection<Ticket> AssignedTickets { get; set; } // Renamed from AssignedTasks

        // Navigation property for TicketComments made by this user
        public ICollection<TicketComment> TicketComments { get; set; } // Renamed from TaskComments

        // Navigation property for Projects created by this user
        public ICollection<Project> CreatedProjects { get; set; }


        public User()
        {
            // Initialize collections to prevent null reference exceptions
            CreatedTickets = new HashSet<Ticket>(); // Renamed
            AssignedTickets = new HashSet<Ticket>(); // Renamed
            TicketComments = new HashSet<TicketComment>(); // Renamed
            CreatedProjects = new HashSet<Project>();
        }
    }
}
