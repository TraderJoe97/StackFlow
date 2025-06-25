using StackFlow.Models;

namespace StackFlow.ViewModels
{
    public class ProjectReportViewModel
    {
        public Project Project { get; set; }
        public int TotalTickets { get; set; }
        public int CompletedTickets { get; set; }
        public int InProgressTickets { get; set; }
        public int ToDoTickets { get; set; }
        public int InReviewTickets { get; set; }
    }
}
