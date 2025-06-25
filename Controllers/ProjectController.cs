using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StackFlow.Data;
using StackFlow.Models;
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
    public class ProjectController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;

        public ProjectController(AppDbContext context, IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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

                    return RedirectToAction("Index", "Dashboard"); // Redirect to Dashboard Index
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
                                        .Include(p => p.Tickets)
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
            return RedirectToAction("Index", "Dashboard"); // Redirect to Dashboard Index
        }
    }
}
