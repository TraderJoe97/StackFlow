using System;

namespace StackFlow.Models
{
    public class TicketComment // Renamed from TaskComment
    {
        public int Id { get; set; }

        // Foreign key to the Ticket this comment belongs to
        public int TicketId { get; set; } // Renamed from TaskId
        public Ticket Ticket { get; set; } // Renamed from Task

        // Foreign key to the User who made this comment
        public int UserId { get; set; }
        public User User { get; set; }

        public string CommentText { get; set; }
        public DateTime CommentCreatedAt { get; set; } = DateTime.UtcNow;
    }
}
