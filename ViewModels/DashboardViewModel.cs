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

        // Lists of tasks categorized by status
        public List<StackFlow.Models.Task> ToDoTasks { get; set; } = new List<StackFlow.Models.Task>();
        public List<StackFlow.Models.Task> InProgressTasks { get; set; } = new List<StackFlow.Models.Task>();
        public List<StackFlow.Models.Task> CompletedTasks { get; set; } = new List<StackFlow.Models.Task>();
        public List<StackFlow.Models.Task> AssignedToMeTasks { get; set; } = new List<StackFlow.Models.Task>(); // Tasks specifically assigned to the current user

        // List of all projects the user is involved in or can see
        public List<Project> Projects { get; set; } = new List<Project>();

        // Constructor to ensure lists are initialized
        public DashboardViewModel()
        {
            // Lists are initialized by default above
        }
    }

    /// <summary>
    /// ViewModel for displaying a single Task with its comments on the dashboard.
    /// </summary>
    public class TaskViewModel : StackFlow.Models.Task
    {
        public List<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public string AssignedToUsername { get; set; }
        public string ProjectName { get; set; }
        public string CreatedByUsername { get; set; }
    }

    /// <summary>
    /// ViewModel for Project Reports (simplistic for now)
    /// </summary>
    public class ProjectReportViewModel
    {
        public Project Project { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int ToDoTasks { get; set; }
        public List<TaskViewModel> Tasks { get; set; } = new List<TaskViewModel>();
    }

    /// <summary>
    /// ViewModel for User Reports (simplistic for now)
    /// </summary>
    public class UserReportViewModel
    {
        public User User { get; set; }
        public int TotalTasksAssigned { get; set; }
        public int CompletedTasksAssigned { get; set; }
        public int InProgressTasksAssigned { get; set; }
        public int ToDoTasksAssigned { get; set; }
        public List<TaskViewModel> AssignedTasks { get; set; } = new List<TaskViewModel>();
    }
}
