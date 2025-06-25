using StackFlow.Models;
using System.Collections.Generic;

namespace StackFlow.ViewModels
{
    public class DashboardViewModel
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public int CurrentUserId { get; set; }

        public List<Ticket> AssignedToMeTickets { get; set; }
        public List<Ticket> ToDoTickets { get; set; }
        public List<Ticket> InProgressTickets { get; set; }
        public List<Ticket> InReviewTickets { get; set; }
        public List<Ticket> CompletedTickets { get; set; }

        public List<Project> Projects { get; set; }
    }
}
