using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackFlow.Data;
using StackFlow.ViewModels;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic; // Added for SelectList

namespace StackFlow.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Displays the main dashboard with an overview of tickets and projects for the current user.
        /// Quick insights are only visible to Admin users.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var currentUser = await _context.Users
                                            .Include(u => u.Role)
                                            .FirstOrDefaultAsync(u => u.Id == currentUserId);

            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Fetch all tickets, eager loading related Project, AssignedTo, and CreatedBy users
            var allTickets = await _context.Tickets // Reverted from Tasks
                                         .Include(t => t.Project)
                                         .Include(t => t.AssignedTo)
                                         .Include(t => t.CreatedBy) // Reverted from TaskCreatedBy
                                         .ToListAsync();

            var allProjects = await _context.Projects
                                            .Include(p => p.CreatedBy)
                                            .ToListAsync();

            var viewModel = new DashboardViewModel
            {
                Username = currentUser.Username,
                Role = currentUser.Role?.Title ?? "Unknown Role",
                CurrentUserId = currentUserId,
                AssignedToMeTickets = allTickets.Where(t => t.AssignedToUserId == currentUserId).ToList(),
                ToDoTickets = allTickets.Where(t => t.Status == "To Do").ToList(), // Reverted from TaskStatus
                InProgressTickets = allTickets.Where(t => t.Status == "In Progress").ToList(), // Reverted from TaskStatus
                InReviewTickets = allTickets.Where(t => t.Status == "In Review").ToList(), // Reverted from TaskStatus
                CompletedTickets = allTickets.Where(t => t.Status == "Done").ToList(), // Reverted from TaskStatus
                Projects = allProjects
            };

            return View(viewModel);
        }

        /// <summary>
        /// Returns the HTML content for the Quick Insights section.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")] // Only admins see insights, so this should also be authorized
        public async Task<IActionResult> GetQuickInsights()
        {
            var allTickets = await _context.Tickets.ToListAsync(); // Reverted from Tasks

            var viewModel = new DashboardViewModel
            {
                ToDoTickets = allTickets.Where(t => t.Status == "To Do").ToList(), // Reverted from TaskStatus
                InProgressTickets = allTickets.Where(t => t.Status == "In Progress").ToList(), // Reverted from TaskStatus
                InReviewTickets = allTickets.Where(t => t.Status == "In Review").ToList(), // Reverted from TaskStatus
                CompletedTickets = allTickets.Where(t => t.Status == "Done").ToList() // Reverted from TaskStatus
            };
            return PartialView("_QuickInsightsPartial", viewModel);
        }

        /// <summary>
        /// Returns the HTML content for the "My Assigned Tickets" table.
        /// </summary>
        /// <param name="userId">The ID of the user whose tickets to fetch.</param>
        [HttpGet]
        public async Task<IActionResult> GetAssignedTicketsTable(int userId)
        {
            var assignedTickets = await _context.Tickets // Reverted from Tasks
                                             .Include(t => t.Project)
                                             .Where(t => t.AssignedToUserId == userId)
                                             .ToListAsync();
            return PartialView("_AssignedTicketsTablePartial", assignedTickets);
        }

        /// <summary>
        /// Returns the HTML content for the "All Projects Overview" section.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProjectsOverview()
        {
            var projects = await _context.Projects
                                        .Include(p => p.CreatedBy)
                                        .ToListAsync();
            return PartialView("_ProjectsOverviewPartial", projects);
        }

        /// <summary>
        /// Retrieves the HTML content for the sidebar navigation.
        /// Used for AJAX updates when user roles change.
        /// </summary>
        [HttpGet]
        public IActionResult GetSidebarContent()
        {
            return PartialView("_SidebarPartial");
        }
    }
}
