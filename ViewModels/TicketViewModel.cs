using StackFlow.Models;
using System;
using System.Collections.Generic;

namespace StackFlow.ViewModels
{
    public class TicketViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; }
        public int? AssignedToUserId { get; set; }
        public User AssignedTo { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }

        public ICollection<TicketComment> Comments { get; set; }

        // Helper properties for display
        public string AssignedToUsername { get; set; }
        public string ProjectName { get; set; }
        public string CreatedByUsername { get; set; }
    }
}
