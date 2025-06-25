using StackFlow.Models;
using System.Collections.Generic;

namespace StackFlow.ViewModels
{
    public class UserReportViewModel
    {
        public User User { get; set; }
        public int TotalTicketsAssigned { get; set; }
        public int CompletedTicketsAssigned { get; set; }
        public int InProgressTicketsAssigned { get; set; }
        public int ToDoTicketsAssigned { get; set; }
        public int InReviewTicketsAssigned { get; set; }

        // Optionally, if you want to display the actual list of tickets in the report view
        public List<TicketViewModel> AssignedTickets { get; set; }
    }
}
