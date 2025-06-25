using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StackFlow.Data;
using StackFlow.Models;
using StackFlow.ViewModels; // Ensure this is present
using StackFlow.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace StackFlow.Controllers
{
    [Authorize] // All actions in this controller require authentication
    public class TicketController : Controller // Reverted from TaskController, if it was named that
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;

        public TicketController(AppDbContext context, IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Displays the form for creating a new ticket.
        /// Accessible only to Admin and Project Managers.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> CreateTicket() // Reverted from CreateTask
        {
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName");
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username");
            ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }); // Reverted from TaskStatuses
            ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }); // Reverted from TaskPriorities
            return View(new Ticket()); // Reverted from Task
        }

        /// <summary>
        /// Handles the submission of the new ticket creation form.
        /// Accessible only to Admin and Project Managers.
        /// </summary>
        /// <param name="ticket">The Ticket model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> CreateTicket([Bind("Title,Description,ProjectId,AssignedToUserId,Status,Priority,DueDate")] Ticket ticket) // Reverted model binding properties
        {
            // Remove navigation properties from ModelState validation, as they are not posted from the form
            ModelState.Remove("Project");
            ModelState.Remove("AssignedTo");
            ModelState.Remove("Comments"); // Reverted from TaskComments
            ModelState.Remove("CreatedBy"); // Reverted from TaskCreatedBy
            ModelState.Remove("CreatedByUserId"); // Reverted from TaskCreatedByUserId
            ModelState.Remove("CreatedAt"); // Reverted from TaskCreatedAt
            ModelState.Remove("CompletedAt"); // Reverted from TaskCompletedAt


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

                    return RedirectToAction("Index", "Dashboard"); // Redirect to Dashboard Index
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error creating ticket: {ex.Message}";
                    ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
                    ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", ticket.AssignedToUserId);
                    ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status); // Reverted
                    ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority); // Reverted
                    return View(ticket);
                }
            }

            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", ticket.AssignedToUserId);
            ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status); // Reverted
            ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority); // Reverted

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
        /// Displays the detailed view of a single ticket.
        /// Accessible to all authenticated users.
        /// </summary>
        /// <param name="id">The ID of the ticket to view.</param>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> TicketDetails(int id) // Reverted from TaskDetails
        {
            var ticket = await _context.Tickets // Reverted from Tasks
                                     .Include(t => t.Project)
                                     .Include(t => t.AssignedTo)
                                     .Include(t => t.CreatedBy) // Reverted from TaskCreatedBy
                                     .Include(t => t.Comments) // Reverted from TaskComments
                                        .ThenInclude(tc => tc.User)
                                     .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            var ticketViewModel = new TicketViewModel // Reverted from TaskViewModel
            {
                Id = ticket.Id,
                Title = ticket.Title, // Reverted
                Description = ticket.Description, // Reverted
                ProjectId = ticket.ProjectId,
                Project = ticket.Project,
                AssignedToUserId = ticket.AssignedToUserId,
                AssignedTo = ticket.AssignedTo,
                Status = ticket.Status, // Reverted
                Priority = ticket.Priority, // Reverted
                CreatedByUserId = ticket.CreatedByUserId,
                CreatedBy = ticket.CreatedBy, // Reverted
                CreatedAt = ticket.CreatedAt, // Reverted
                DueDate = ticket.DueDate, // Reverted
                CompletedAt = ticket.CompletedAt, // Reverted
                Comments = ticket.Comments.OrderBy(c => c.CommentCreatedAt).ToList(), // Reverted
                AssignedToUsername = ticket.AssignedTo?.Username,
                ProjectName = ticket.Project?.ProjectName,
                CreatedByUsername = ticket.CreatedBy?.Username // Reverted
            };

            // Force a default selected value for the dropdown if Model.Status is null or empty
            var currentStatus = string.IsNullOrEmpty(ticket.Status) ? "To Do" : ticket.Status;

            // Create a list of SelectListItem explicitly to ensure values are set
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
        public async Task<IActionResult> UpdateTicketStatus(int ticketId, string newStatus) // Reverted from UpdateTaskStatus
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                TempData["ErrorMessage"] = "Authentication error: Could not identify user for status update.";
                return Unauthorized();
            }

            var ticket = await _context.Tickets.FindAsync(ticketId); // Reverted from Tasks.FindAsync
            if (ticket == null)
            {
                return NotFound();
            }

            // Trim whitespace from newStatus for robust comparison
            newStatus = newStatus?.Trim();

            var allowedStatuses = new List<string> { "To Do", "In Progress", "In Review", "Done" };
            if (!allowedStatuses.Contains(newStatus))
            {
                TempData["ErrorMessage"] = $"Invalid ticket status provided: '{newStatus}'. Expected one of: {string.Join(", ", allowedStatuses)}.";
                return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
            }

            string oldStatus = ticket.Status; // Reverted
            ticket.Status = newStatus; // Reverted
            if (newStatus == "Done" && !ticket.CompletedAt.HasValue) // Reverted
            {
                ticket.CompletedAt = DateTime.UtcNow; // Reverted
            }
            else if (newStatus != "Done" && ticket.CompletedAt.HasValue) // Reverted
            {
                ticket.CompletedAt = null; // Reverted
            }

            try
            {
                _context.Update(ticket);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Ticket '{ticket.Title}' status updated to '{newStatus}' successfully!";

                // Send minimal data: action and ticket ID
                await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "updated", ticket.Id, oldStatus);

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
        public async Task<IActionResult> AddComment(int ticketId, string commentText) // Reverted from AddComment
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

            var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId); // Reverted from Tasks.FirstOrDefaultAsync
            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Ticket not found.";
                return RedirectToAction("Index", "Dashboard"); // Redirect to Dashboard Index
            }

            var ticketComment = new TicketComment // Reverted from TaskComment
            {
                TicketId = ticketId, // Reverted
                UserId = currentUserId,
                CommentText = commentText.Trim(),
                CommentCreatedAt = DateTime.UtcNow
            };

            _context.TicketComments.Add(ticketComment); // Reverted
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Comment added successfully!";
            // Comments are not being updated in the main dashboard view, so a simple notification is fine.
            await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "commented", ticket.Id);

            return RedirectToAction(nameof(TicketDetails), new { id = ticketId });
        }

        /// <summary>
        /// Displays the form for editing an existing ticket.
        /// Accessible only to Admin and Project Managers.
        /// </summary>
        /// <param name="id">The ID of the ticket to edit.</param>
        [HttpGet]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> EditTicket(int id) // Reverted from EditTask
        {
            var ticket = await _context.Tickets.FindAsync(id); // Reverted from Tasks.FindAsync
            if (ticket == null)
            {
                return NotFound();
            }

            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username");
            ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status); // Reverted
            ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority); // Reverted

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
        public async Task<IActionResult> EditTicket(int id, [Bind("Id,Title,Description,ProjectId,AssignedToUserId,Status,Priority,DueDate,CompletedAt")] Ticket ticket) // Reverted model binding properties
        {
            if (id != ticket.Id)
            {
                return NotFound();
            }

            // Remove navigation properties from ModelState validation, as they are not posted from the form
            ModelState.Remove("Project");
            ModelState.Remove("AssignedTo");
            ModelState.Remove("Comments"); // Reverted
            ModelState.Remove("CreatedBy"); // Reverted
            ModelState.Remove("CreatedByUserId"); // Reverted
            ModelState.Remove("CreatedAt"); // Reverted


            if (ModelState.IsValid)
            {
                try
                {
                    // Fetch the original ticket to preserve CreatedByUserId and CreatedAt
                    var originalTicket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id); // Reverted
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
                    await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "updated", ticket.Id, originalTicket.Status);

                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Tickets.AnyAsync(e => e.Id == ticket.Id)) // Reverted
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
                    ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status); // Reverted
                    ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority); // Reverted
                    return View(ticket);
                }
                return RedirectToAction("Index", "Dashboard"); // Redirect to Dashboard Index
            }

            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", ticket.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", ticket.AssignedToUserId);
            ViewBag.TicketStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, ticket.Status); // Reverted
            ViewBag.TicketPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, ticket.Priority); // Reverted

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
        [HttpPost, ActionName("DeleteTicket")] // Reverted from DeleteTask
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> DeleteTicketConfirmed(int id) // Reverted from DeleteTaskConfirmed
        {
            var ticket = await _context.Tickets.FindAsync(id); // Reverted from Tasks.FindAsync
            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Ticket not found.";
                return NotFound();
            }

            string ticketTitle = ticket.Title; // Reverted
            string oldStatus = ticket.Status; // Reverted

            try
            {
                _context.Tickets.Remove(ticket); // Reverted
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Ticket '{ticketTitle}' deleted successfully.";
                // Send minimal data: action and ticket ID
                await _hubContext.Clients.All.SendAsync("ReceiveTicketUpdate", "deleted", id, oldStatus);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting ticket: {ex.Message}";
            }
            return RedirectToAction("Index", "Dashboard"); // Redirect to Dashboard Index
        }
    }
}
