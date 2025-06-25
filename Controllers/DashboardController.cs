using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StackFlow.Data;
using StackFlow.Models;
using StackFlow.ViewModels;
using StackFlow.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace StackFlow.Controllers
{
    // All actions in this controller require authentication
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;

        public DashboardController(AppDbContext context, IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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

            // Fetch all tickets for insights and assigned tickets table
            var allTickets = await _context.Tickets
                                         .Include(t => t.Project) // Needed for AssignedToMeTickets display
                                         .Include(t => t.AssignedTo)
                                         .Include(t => t.CreatedBy)
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
                ToDoTickets = allTickets.Where(t => t.Status == "To Do").ToList(),
                InProgressTickets = allTickets.Where(t => t.Status == "In Progress").ToList(),
                InReviewTickets = allTickets.Where(t => t.Status == "In Review").ToList(),
                CompletedTickets = allTickets.Where(t => t.Status == "Done").ToList(),
                Projects = allProjects
            };

            return View(viewModel);
        }

        /// <summary>
        /// Returns the HTML content for the Quick Insights section.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetQuickInsights()
        {
            // Re-fetch data needed for insights
            var allTickets = await _context.Tickets.ToListAsync();

            var viewModel = new DashboardViewModel
            {
                ToDoTickets = allTickets.Where(t => t.Status == "To Do").ToList(),
                InProgressTickets = allTickets.Where(t => t.Status == "In Progress").ToList(),
                InReviewTickets = allTickets.Where(t => t.Status == "In Review").ToList(),
                CompletedTickets = allTickets.Where(t => t.Status == "Done").ToList()
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
            var assignedTickets = await _context.Tickets
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

        /// <summary>
        /// Displays the form for creating a new ticket.
        /// Accessible only to Admin and Project Managers.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> CreateTicket()
        {
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName");
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username");
            ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" });
            ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" });
            return View(new Ticket());
        }

        /// <summary>
        /// Handles the submission of the new ticket creation form.
        /// Accessible only to Admin and Project Managers.
        /// </summary>
        /// <param name="ticket">The Ticket model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> CreateTicket([Bind("Title,Description,ProjectId,AssignedToUserId,Status,Priority,DueDate")] Ticket ticket)
        {
            // Remove navigation properties from ModelState validation, as they are not posted from the form
            ModelState.Remove("Project");
            ModelState.Remove("AssignedTo");
            ModelState.Remove("Comments");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("CreatedAt");

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                TempData["ErrorMessage"] = "Authentication error: Could not identify user for ticket creation.";
                return RedirectToAction("Login", "Account");
            }

            ticket.CreatedByUserId = currentUserId;
            ticket.CreatedAt = DateTime.UtcNow;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(ticket);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Ticket '{ticket.Title}' created successfully!";

                    // Send minimal data: action and ticket ID
                    await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "created", ticket.Id);

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error creating ticket: {ex.Message}";
                    ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
                    ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", ticket.AssignedToUserId);
                    ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status);
                    ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority);
                    return View(ticket);
                }
            }

            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", ticket.AssignedToUserId);
            ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status);
            ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority);

            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    Console.WriteLine($"Validation Error (CreateTicket): {error.ErrorMessage}");
                }
            }
            return View(ticket);
        }

        /// <summary>
        /// Displays the form for creating a new project.
        /// Accessible only to Admin users.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateProject()
        {
            ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" });
            return View(new Project());
        }

        /// <summary>
        /// Handles the submission of the new project creation form.
        /// Accessible only to Admin users.
        /// </summary>
        /// <param name="project">The Project model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProject([Bind("ProjectName,ProjectDescription,ProjectStartDate,ProjectEndDate,ProjectStatus")] Project project)
        {
            ModelState.Remove("CreatedBy");
            ModelState.Remove("Tickets");
            ModelState.Remove("CreatedByUserId");

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                TempData["ErrorMessage"] = "Authentication error: Could not identify user.";
                return RedirectToAction("Login", "Account");
            }

            project.CreatedByUserId = currentUserId;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(project);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Project '{project.ProjectName}' created successfully!";

                    // Send minimal data: action and project ID
                    await _hubContext.Clients.All.SendAsync("ReceiveProjectUpdate", "created", project.Id);

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error creating project: {ex.Message}";
                    ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" }, project.ProjectStatus);
                    return View(project);
                }
            }
            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" }, project.ProjectStatus);

            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    Console.WriteLine($"Validation Error (CreateProject): {error.ErrorMessage}");
                }
            }
            return View(project);
        }

        /// <summary>
        /// Displays the detailed view of a single ticket, including its comments.
        /// Accessible to all authenticated users.
        /// </summary>
        /// <param name="id">The ID of the ticket to view.</param>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> TicketDetails(int id)
        {
            var ticket = await _context.Tickets
                                     .Include(t => t.Project)
                                     .Include(t => t.AssignedTo)
                                     .Include(t => t.CreatedBy)
                                     .Include(t => t.Comments)
                                        .ThenInclude(tc => tc.User)
                                     .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            var ticketViewModel = new TicketViewModel
            {
                Id = ticket.Id,
                Title = ticket.Title,
                Description = ticket.Description,
                ProjectId = ticket.ProjectId,
                Project = ticket.Project,
                AssignedToUserId = ticket.AssignedToUserId,
                AssignedTo = ticket.AssignedTo,
                Status = ticket.Status,
                Priority = ticket.Priority,
                CreatedByUserId = ticket.CreatedByUserId,
                CreatedBy = ticket.CreatedBy,
                CreatedAt = ticket.CreatedAt,
                DueDate = ticket.DueDate,
                CompletedAt = ticket.CompletedAt,
                Comments = ticket.Comments.OrderBy(c => c.CommentCreatedAt).ToList(),
                AssignedToUsername = ticket.AssignedTo?.Username,
                ProjectName = ticket.Project?.ProjectName,
                CreatedByUsername = ticket.CreatedBy?.Username
            };

            // Force a default selected value for the dropdown if Model.Status is null or empty
            var currentStatus = string.IsNullOrEmpty(ticket.Status) ? "To Do" : ticket.Status;

            // FIX: Create a list of SelectListItem explicitly to ensure values are set
            var statusOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "To Do", Text = "To Do", Selected = (currentStatus == "To Do") },
                new SelectListItem { Value = "In Progress", Text = "In Progress", Selected = (currentStatus == "In Progress") },
                new SelectListItem { Value = "In Review", Text = "In Review", Selected = (currentStatus == "In Review") },
                new SelectListItem { Value = "Done", Text = "Done", Selected = (currentStatus == "Done") }
            };
            ViewBag.TicketStatuses = new SelectList(statusOptions, "Value", "Text", currentStatus);


            return View(ticketViewModel);
        }

        /// <summary>
        /// Handles the submission for updating a ticket's status from the Ticket Details page.
        /// Accessible to all authenticated users.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateTicketStatus(int ticketId, string newStatus)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                TempData["ErrorMessage"] = "Authentication error: Could not identify user for status update.";
                return Unauthorized();
            }

            var ticket = await _context.Tickets.FindAsync(ticketId);
            if (ticket == null)
            {
                return NotFound();
            }

            // Trim whitespace from newStatus for robust comparison
            newStatus = newStatus?.Trim();

            var allowedStatuses = new List<string> { "To Do", "In Progress", "In Review", "Done" };
            if (!allowedStatuses.Contains(newStatus))
            {
                // Enhance error message to show the problematic value
                TempData["ErrorMessage"] = $"Invalid ticket status provided: '{newStatus}'. Expected one of: {string.Join(", ", allowedStatuses)}.";
                return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
            }

            string oldStatus = ticket.Status; // Store old status to pass with notification
            ticket.Status = newStatus;
            if (newStatus == "Done" && !ticket.CompletedAt.HasValue)
            {
                ticket.CompletedAt = DateTime.UtcNow;
            }
            else if (newStatus != "Done" && ticket.CompletedAt.HasValue)
            {
                ticket.CompletedAt = null;
            }

            try
            {
                _context.Update(ticket);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Ticket '{ticket.Title}' status updated to '{newStatus}' successfully!";

                // Send minimal data: action and ticket ID
                await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "updated", ticket.Id, oldStatus); // Pass old status too for insights refresh

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating ticket status: {ex.Message}";
            }

            return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
        }


        /// <summary>
        /// Handles the submission for adding a new comment to a ticket.
        /// Accessible to all authenticated users.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> AddComment(int ticketId, string commentText)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(commentText))
            {
                ModelState.AddModelError("commentText", "Comment cannot be empty.");
                TempData["ErrorMessage"] = "Comment cannot be empty.";
                return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
            }

            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Ticket not found.";
                return RedirectToAction(nameof(Index));
            }

            var ticketComment = new TicketComment
            {
                TicketId = ticketId,
                UserId = currentUserId,
                CommentText = commentText.Trim(),
                CommentCreatedAt = DateTime.UtcNow
            };

            _context.TicketComments.Add(ticketComment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Comment added successfully!";
            // Comments update isn't on dashboard. Simple notification is fine.
            await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "commented", ticket.Id); // Notify that a comment was added

            return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
        }

        /// <summary>
        /// Displays the form for editing an existing ticket.
        /// Accessible only to Admin and Project Managers.
        /// </summary>
        /// <param name="id">The ID of the ticket to edit.</param>
        [HttpGet]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> EditTicket(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                return NotFound();
            }

            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username");
            ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status);
            ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority);

            return View(ticket);
        }

        /// <summary>
        /// Handles the submission of the edited ticket form.
        /// Accessible only to Admin and Project Managers.
        /// </summary>
        /// <param name="id">The ID of the ticket being edited.</param>
        /// <param name="ticket">The Ticket model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> EditTicket(int id, [Bind("Id,Title,Description,ProjectId,AssignedToUserId,Status,Priority,DueDate,CompletedAt")] Ticket ticket)
        {
            if (id != ticket.Id)
            {
                return NotFound();
            }

            // Remove navigation properties from ModelState validation, as they are not posted from the form
            ModelState.Remove("Project");
            ModelState.Remove("AssignedTo");
            ModelState.Remove("Comments");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("CreatedAt");


            if (ModelState.IsValid)
            {
                try
                {
                    // Fetch the original ticket to preserve CreatedByUserId and CreatedAt
                    var originalTicket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
                    if (originalTicket == null)
                    {
                        return NotFound();
                    }
                    ticket.CreatedByUserId = originalTicket.CreatedByUserId;
                    ticket.CreatedAt = originalTicket.CreatedAt;


                    _context.Update(ticket);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Ticket '{ticket.Title}' updated successfully!";

                    // Send minimal data: action and ticket ID
                    await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "updated", ticket.Id, originalTicket.Status); // Pass old status

                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Tickets.AnyAsync(e => e.Id == ticket.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating ticket: {ex.Message}";
                    ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
                    ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", ticket.AssignedToUserId);
                    ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status);
                    ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority);
                    return View(ticket);
                }
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", ticket.AssignedToUserId);
            ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status);
            ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority);

            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    Console.WriteLine($"Validation Error (EditTicket): {error.ErrorMessage}");
                }
            }
            return View(ticket);
        }

        /// <summary>
        /// POST action to delete a ticket.
        /// Accessible only to Admin and Project Managers.
        /// </summary>
        /// <param name="id">The ID of the ticket to delete.</param>
        [HttpPost, ActionName("DeleteTicket")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> DeleteTicketConfirmed(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Ticket not found.";
                return NotFound();
            }

            string ticketTitle = ticket.Title;
            string oldStatus = ticket.Status; // Store old status for insights update

            try
            {
                _context.Tickets.Remove(ticket);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Ticket '{ticketTitle}' deleted successfully.";
                // Send minimal data: action and ticket ID
                await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "deleted", id, oldStatus);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting ticket: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Displays the detailed view of a single project, including its associated tickets.
        /// Accessible to all authenticated users.
        /// </summary>
        /// <param name="id">The ID of the project to view.</param>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ProjectDetails(int id)
        {
            var project = await _context.Projects
                                        .Include(p => p.CreatedBy)
                                        .Include(p => p.Tickets) // Eager load associated tickets
                                        .FirstOrDefaultAsync(m => m.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            return View(project);
        }

        /// <summary>
        /// POST action to delete a project and its associated tickets.
        /// Accessible only to Admin users.
        /// </summary>
        /// <param name="id">The ID of the project to delete.</param>
        [HttpPost, ActionName("DeleteProject")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProjectConfirmed(int id)
        {
            var project = await _context.Projects
                                        .Include(p => p.Tickets)
                                        .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                TempData["ErrorMessage"] = "Project not found.";
                return NotFound();
            }

            string projectName = project.ProjectName;
            int ticketCount = project.Tickets.Count;

            try
            {
                // Remove associated tickets first if not configured for cascade delete in DB
                _context.Tickets.RemoveRange(project.Tickets);
                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Project '{projectName}' and its {ticketCount} associated tickets deleted successfully.";
                // Send minimal data: action and project ID
                await _hubContext.Clients.All.SendAsync("ReceiveProjectUpdate", "deleted", id);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting project: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Displays a report of projects with their ticket statistics.
        /// Accessible only to Admin users.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ProjectReports()
        {
            var projects = await _context.Projects
                                         .Include(p => p.Tickets)
                                         .OrderBy(p => p.ProjectName)
                                         .ToListAsync();

            var projectReports = projects.Select(p => new ProjectReportViewModel
            {
                Project = p,
                TotalTickets = p.Tickets.Count,
                CompletedTickets = p.Tickets.Count(t => t.Status == "Done"),
                InProgressTickets = p.Tickets.Count(t => t.Status == "In Progress"),
                ToDoTickets = p.Tickets.Count(t => t.Status == "To Do"),
                InReviewTickets = p.Tickets.Count(t => t.Status == "In Review")
            }).ToList();

            return View(projectReports);
        }

        /// <summary>
        /// Displays a report of all users and their assigned ticket statistics.
        /// Accessible only to Admin users.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UserReports()
        {
            var users = await _context.Users
                                      .Include(u => u.AssignedTickets)
                                      .Include(u => u.Role)
                                      .OrderBy(u => u.Username)
                                      .ToListAsync();

            var userReports = users.Select(u => new UserReportViewModel
            {
                User = u,
                TotalTicketsAssigned = u.AssignedTickets.Count,
                CompletedTicketsAssigned = u.AssignedTickets.Count(t => t.Status == "Done"),
                InProgressTicketsAssigned = u.AssignedTickets.Count(t => t.Status == "In Progress"),
                AssignedTickets = u.AssignedTickets.Select(t => new TicketViewModel
                {
                    Id = t.Id,
                    Title = t.Title,
                    Status = t.Status,
                    Priority = t.Priority,
                    DueDate = t.DueDate
                }).ToList(),
                ToDoTicketsAssigned = u.AssignedTickets.Count(t => t.Status == "To Do"),
                InReviewTicketsAssigned = u.AssignedTickets.Count(t => t.Status == "In Review")
            }).ToList();

            // Fetch all roles for the "Change Role" dropdown
            ViewBag.Roles = new SelectList(await _context.Roles.ToListAsync(), "Id", "Title");

            return View(userReports);
        }

        /// <summary>
        /// POST action to update a user's role.
        /// Accessible only to Admin users.
        /// </summary>
        /// <param name="userId">The ID of the user whose role is to be updated.</param>
        /// <param name="newRoleId">The ID of the new role.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUserRole(int userId, int newRoleId)
        {
            var userToUpdate = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == userId);
            if (userToUpdate == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return NotFound();
            }

            var newRole = await _context.Roles.FindAsync(newRoleId);
            if (newRole == null)
            {
                TempData["ErrorMessage"] = "New role not found.";
                return NotFound();
            }

            // Prevent an admin from changing their own role (optional but recommended)
            var currentAdminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(currentAdminIdString, out int currentAdminId) && currentAdminId == userId)
            {
                TempData["ErrorMessage"] = "You cannot change your own role.";
                return RedirectToAction(nameof(UserReports));
            }

            string oldRoleTitle = userToUpdate.Role?.Title ?? "Unknown";
            userToUpdate.RoleId = newRoleId;

            try
            {
                _context.Update(userToUpdate);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"User '{userToUpdate.Username}' role updated from '{oldRoleTitle}' to '{newRole.Title}' successfully.";

                // Send minimal data: action and user ID
                await _hubContext.Clients.All.SendAsync("ReceiveUserUpdate", "roleUpdated", userId);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating user role: {ex.Message}";
                // Log the exception
            }
            return RedirectToAction(nameof(UserReports));
        }

        /// <summary>
        /// POST action to delete a user and reassign their tickets to the deleting admin.
        /// Accessible only to Admin users.
        /// </summary>
        /// <param name="id">The ID of the user to delete.</param>
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUserConfirmed(int id)
        {
            var userToDelete = await _context.Users
                                             .Include(u => u.AssignedTickets)
                                             .FirstOrDefaultAsync(u => u.Id == id);

            if (userToDelete == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return NotFound();
            }

            // Prevent an admin from deleting themselves (optional but recommended)
            var currentAdminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(currentAdminIdString, out int currentAdminId) && currentAdminId == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(UserReports));
            }

            string username = userToDelete.Username;
            int assignedTicketCount = userToDelete.AssignedTickets.Count;

            try
            {
                // Get the current admin user (who is performing the deletion)
                var adminUser = await _context.Users.FindAsync(currentAdminId);
                if (adminUser == null)
                {
                    TempData["ErrorMessage"] = "Admin user not found for ticket reassignment.";
                    return RedirectToAction(nameof(UserReports));
                }

                // Reassign tickets from the user being deleted to the admin
                foreach (var ticket in userToDelete.AssignedTickets)
                {
                    ticket.AssignedToUserId = adminUser.Id;
                }
                _context.Tickets.UpdateRange(userToDelete.AssignedTickets);

                // Delete the user
                _context.Users.Remove(userToDelete);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"User '{username}' deleted and their tickets reassigned to {adminUser.Username}.";
                // Send minimal data: action and user ID
                await _hubContext.Clients.All.SendAsync("ReceiveUserUpdate", "deleted", id);

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting user: {ex.Message}";
                // Log the exception
            }
            return RedirectToAction(nameof(UserReports));
        }
    }
}
