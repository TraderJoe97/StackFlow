using System;
using System.Collections.Generic;

namespace StackFlow.Models
{
    public class Ticket
    {
        public int Id { get; set; }

        public string Title { get; set; } // Renamed from TicketTitle
        public string Description { get; set; } // Renamed from TicketDescription

        // Foreign key to Project
        public int ProjectId { get; set; }
        public Project Project { get; set; }

        // Foreign key to User who is assigned this ticket
        public int? AssignedToUserId { get; set; }
        public User AssignedTo { get; set; }

        public string Status { get; set; } // Renamed from TicketStatus
        public string Priority { get; set; } // Renamed from TicketPriority

        // Foreign key to User who created this ticket
        public int CreatedByUserId { get; set; } // Renamed from TicketCreatedByUserId
        public User CreatedBy { get; set; } // Renamed from TicketCreatedBy

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Renamed from TicketCreatedAt
        public DateTime? DueDate { get; set; } // Renamed from TicketDueDate
        public DateTime? CompletedAt { get; set; } // Renamed from TicketCompletedAt

        // Navigation property for comments associated with this ticket
        public ICollection<TicketComment> Comments { get; set; } // Renamed from TicketComments

        public Ticket()
        {
            Comments = new HashSet<TicketComment>(); // Initialize collection (renamed from TicketComments)
        }
    }
}
