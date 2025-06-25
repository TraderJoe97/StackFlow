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
    [Authorize(Roles = "Admin")] // All actions in this controller accessible only to Admin
    public class ReportController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;

        public ReportController(AppDbContext context, IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Displays a report of projects with their ticket statistics.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProjectReports()
        {
            var projects = await _context.Projects
                                         .Include(p => p.Tickets) // Reverted from Tasks
                                         .OrderBy(p => p.ProjectName)
                                         .ToListAsync();

            var projectReports = projects.Select(p => new ProjectReportViewModel
            {
                Project = p,
                TotalTickets = p.Tickets.Count, // Reverted from Tasks.Count
                CompletedTickets = p.Tickets.Count(t => t.Status == "Done"), // Reverted from TaskStatus
                InProgressTickets = p.Tickets.Count(t => t.Status == "In Progress"), // Reverted from TaskStatus
                ToDoTickets = p.Tickets.Count(t => t.Status == "To Do"), // Reverted from TaskStatus
                InReviewTickets = p.Tickets.Count(t => t.Status == "In Review") // Reverted from TaskStatus
            }).ToList();

            return View(projectReports);
        }

        /// <summary>
        /// Displays a report of all users and their assigned ticket statistics.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UserReports()
        {
            var users = await _context.Users
                                      .Include(u => u.AssignedTickets) // Reverted from AssignedTasks
                                      .Include(u => u.Role)
                                      .OrderBy(u => u.Username)
                                      .ToListAsync();

            var userReports = users.Select(u => new UserReportViewModel
            {
                User = u,
                TotalTicketsAssigned = u.AssignedTickets.Count, // Reverted from AssignedTasks.Count
                CompletedTicketsAssigned = u.AssignedTickets.Count(t => t.Status == "Done"), // Reverted from TaskStatus
                InProgressTicketsAssigned = u.AssignedTickets.Count(t => t.Status == "In Progress"), // Reverted from TaskStatus
                AssignedTickets = u.AssignedTickets.Select(t => new TicketViewModel
                {
                    Id = t.Id,
                    Title = t.Title, // Reverted from TaskTitle
                    Status = t.Status, // Reverted from TaskStatus
                    Priority = t.Priority, // Reverted from TaskPriority
                    DueDate = t.DueDate // Reverted from TaskDueDate
                }).ToList(),
                ToDoTicketsAssigned = u.AssignedTickets.Count(t => t.Status == "To Do"), // Reverted from TaskStatus
                InReviewTicketsAssigned = u.AssignedTickets.Count(t => t.Status == "In Review") // Reverted from TaskStatus
            }).ToList();

            // Fetch all roles for the "Change Role" dropdown
            ViewBag.Roles = new SelectList(await _context.Roles.ToListAsync(), "Id", "Title");

            return View(userReports);
        }

        /// <summary>
        /// POST action to update a user's role.
        /// </summary>
        /// <param name="userId">The ID of the user whose role is to be updated.</param>
        /// <param name="newRoleId">The ID of the new role.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
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
        /// </summary>
        /// <param name="id">The ID of the user to delete.</param>
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(int id)
        {
            var userToDelete = await _context.Users
                                             .Include(u => u.AssignedTickets) // Reverted from AssignedTasks
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
            int assignedTicketCount = userToDelete.AssignedTickets.Count; // Reverted from assignedTaskCount

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
                foreach (var ticket in userToDelete.AssignedTickets) // Reverted from AssignedTasks
                {
                    ticket.AssignedToUserId = adminUser.Id;
                }
                _context.Tickets.UpdateRange(userToDelete.AssignedTickets); // Reverted from Tasks.UpdateRange

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
