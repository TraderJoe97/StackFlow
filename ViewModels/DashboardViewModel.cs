using System.Collections.Generic;
using StackFlow.Models;

namespace StackFlow.ViewModels
{
    /// <summary>
    /// ViewModel for the main Dashboard page.
    /// Gathers all necessary data to be displayed on the dashboard.
    /// </summary>
    public class DashboardViewModel
    {
        // User-specific information
        public string Username { get; set; }
        public string Role { get; set; }
        public int CurrentUserId { get; set; }

        // Lists of tickets categorized by status (formerly tasks)
        public List<Ticket> ToDoTickets { get; set; } = new List<Ticket>(); // Changed from ToDoTasks
        public List<Ticket> InProgressTickets { get; set; } = new List<Ticket>(); // Changed from InProgressTasks
        public List<Ticket> InReviewTickets { get; set; } = new List<Ticket>(); // Changed from InReviewTasks
        public List<Ticket> CompletedTickets { get; set; } = new List<Ticket>(); // Changed from CompletedTasks
        public List<Ticket> AssignedToMeTickets { get; set; } = new List<Ticket>(); // Changed from AssignedToMeTasks

        // List of all projects the user is involved in or can see
        public List<Project> Projects { get; set; } = new List<Project>();

        // Constructor to ensure lists are initialized
        public DashboardViewModel()
        {
            // Lists are initialized by default above
        }
    }

    /// <summary>
    /// ViewModel for displaying a single Ticket with its comments.
    /// Inherits from the Ticket model itself for direct property access.
    /// </summary>
    public class TicketViewModel : Ticket // Inherit directly from Ticket
    {
        public List<TicketComment> Comments { get; set; } = new List<TicketComment>(); // Renamed from TaskComment to TicketComment
        public string AssignedToUsername { get; set; }
        public string ProjectName { get; set; }
        public string CreatedByUsername { get; set; } // Renamed from TaskCreatedByUsername
    }

    /// <summary>
    /// ViewModel for Project Reports
    /// </summary>
    public class ProjectReportViewModel
    {
        public Project Project { get; set; }
        public int TotalTickets { get; set; } // Changed from TotalTasks
        public int CompletedTickets { get; set; } // Changed from CompletedTasks
        public int InProgressTickets { get; set; } // Changed from InProgressTasks
        public int ToDoTickets { get; set; } // Changed from ToDoTasks
        public int InReviewTickets { get; set; } // Changed from InReviewTasks
        public List<TicketViewModel> Tickets { get; set; } = new List<TicketViewModel>(); // Changed from Tasks
    }

    /// <summary>
    /// ViewModel for User Reports
    /// </summary>
    public class UserReportViewModel
    {
        public User User { get; set; }
        public int TotalTicketsAssigned { get; set; } // Changed from TotalTasksAssigned
        public int CompletedTicketsAssigned { get; set; } // Changed from CompletedTasksAssigned
        public int InProgressTicketsAssigned { get; set; } // Changed from InProgressTasksAssigned
        public int ToDoTicketsAssigned { get; set; } // Changed from ToDoTasksAssigned
        public int InReviewTicketsAssigned { get; set; } // Changed from InReviewTasksAssigned
        public List<TicketViewModel> AssignedTickets { get; set; } = new List<TicketViewModel>(); // Changed from AssignedTasks
    }
}
