using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Needed for SelectList
using Microsoft.EntityFrameworkCore;
using StackFlow.Data;
using StackFlow.Models;
using StackFlow.ViewModels;
using System; // Added for Exception
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic; // Added for List<string>

namespace StackFlow.Controllers
{
    // Ensures only authenticated users can access the dashboard
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Displays the main dashboard with an overview of tasks and projects for the current user.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Get the current user's ID from claims
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                // If user ID is not found or invalid, redirect to login.
                // This might indicate an authentication issue or corrupted cookie.
                return RedirectToAction("Login", "Account");
            }

            // Fetch the current user along with their role for display on the dashboard.
            var currentUser = await _context.Users
                                            .Include(u => u.Role) // Eager load the Role navigation property
                                            .FirstOrDefaultAsync(u => u.Id == currentUserId);

            if (currentUser == null)
            {
                // If the user is authenticated but not found in the database,
                // it suggests a data inconsistency or deleted user.
                return RedirectToAction("Login", "Account");
            }

            // Fetch all tasks, including their associated Project, AssignedTo User, and CreatedBy User.
            // This ensures all necessary data for task cards is available.
            var allTasks = await _context.Tasks
                                         .Include(t => t.Project)
                                         .Include(t => t.AssignedTo)
                                         .Include(t => t.TaskCreatedBy)
                                         .ToListAsync();

            // Fetch all projects. In a real application, you might filter this
            // based on user involvement (e.g., projects they created or are assigned to).
            var allProjects = await _context.Projects
                                            .Include(p => p.CreatedBy) // Include the user who created the project
                                            .ToListAsync();

            // Populate the DashboardViewModel with the fetched data.
            var viewModel = new DashboardViewModel
            {
                Username = currentUser.Username,
                Role = currentUser.Role?.Title ?? "Unknown Role", // Safely get role title, default if null
                CurrentUserId = currentUserId,
                // Filter tasks assigned to the current user
                AssignedToMeTasks = allTasks.Where(t => t.AssignedToUserId == currentUserId).ToList(),
                // Filter tasks by their status
                ToDoTasks = allTasks.Where(t => t.TaskStatus == "To Do").ToList(),
                InProgressTasks = allTasks.Where(t => t.TaskStatus == "In Progress").ToList(),
                CompletedTasks = allTasks.Where(t => t.TaskStatus == "Done").ToList(), // Changed from "Completed" to "Done"
                // Assign all projects (consider filtering for large datasets)
                Projects = allProjects
            };

            return View(viewModel);
        }

        /// <summary>
        /// Displays the form for creating a new task.
        /// Populates dropdowns for Project and AssignedToUser.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CreateTask()
        {
            // Fetch all projects to populate the Project dropdown
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName");

            // Fetch all users to populate the AssignedTo dropdown
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username");

            // Define consistent Task Statuses and Priorities
            ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" });
            ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" });


            // Initialize a new Task model to bind to the form
            return View(new StackFlow.Models.Task());
        }

        /// <summary>
        /// Handles the submission of the new task creation form.
        /// </summary>
        /// <param name="task">The Task model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask([Bind("TaskTitle,TaskDescription,ProjectId,AssignedToUserId,TaskStatus,TaskPriority,TaskDueDate")] StackFlow.Models.Task task)
        {
            // IMPORTANT: Remove model state errors for navigation properties that are not bound from the form.
            // These errors can arise if the model binder attempts to validate these properties
            // even when they're not part of the input, especially if they're non-nullable
            // and backed by required foreign keys or collections.
            ModelState.Remove("Project"); // Exclude Project navigation property
            ModelState.Remove("AssignedTo"); // Exclude AssignedTo navigation property
            ModelState.Remove("TaskComments"); // Exclude TaskComments navigation property
            ModelState.Remove("TaskCreatedBy"); // Exclude TaskCreatedBy navigation property


            // Get the current user's ID to set as the creator of the task
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                // If user ID is invalid, return unauthorized or redirect to login.
                TempData["ErrorMessage"] = "Authentication error: Could not identify user for task creation.";
                return RedirectToAction("Login", "Account");
            }

            // Manually set properties that are not part of the form binding
            task.TaskCreatedByUserId = currentUserId; // Set this BEFORE ModelState.IsValid check
            task.TaskCreatedAt = DateTime.UtcNow;

            // Validate the model based on data annotations
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(task); // Add the new task to the database context
                    await _context.SaveChangesAsync(); // Save changes
                    TempData["SuccessMessage"] = $"Task '{task.TaskTitle}' created successfully!";
                    return RedirectToAction(nameof(Index)); // Redirect to dashboard on success
                }
                catch (Exception ex)
                {
                    // Log the exception (e.g., using ILogger)
                    TempData["ErrorMessage"] = $"Error creating task: {ex.Message}";
                    // Re-populate ViewBags before returning the view
                    ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", task.ProjectId);
                    ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", task.AssignedToUserId);
                    ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, task.TaskStatus);
                    ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, task.TaskPriority);
                    return View(task); // Stay on the create page with error
                }
            }

            // If ModelState is not valid, re-populate ViewBag and return the view with errors
            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", task.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", task.AssignedToUserId);
            ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, task.TaskStatus);
            ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, task.TaskPriority);

            // --- DEBUGGING: Print validation errors to console ---
            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    Console.WriteLine($"Validation Error (CreateTask): {error.ErrorMessage}");
                }
            }
            // --- END DEBUGGING ---

            return View(task); // Return the view with validation errors
        }

        /// <summary>
        /// Displays the form for creating a new project.
        /// </summary>
        [HttpGet]
        public IActionResult CreateProject()
        {
            // Define consistent Project Statuses based on DB CHECK constraint
            ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" });
            return View(new Project()); // Return a new Project model
        }

        /// <summary>
        /// Handles the submission of the new project creation form.
        /// </summary>
        /// <param name="project">The Project model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updated [Bind] attribute to exclude navigation properties 'CreatedBy' and 'Tasks'
        public async Task<IActionResult> CreateProject([Bind("ProjectName,ProjectDescription,ProjectStartDate,ProjectEndDate,ProjectStatus")] Project project)
        {
            // IMPORTANT: Remove model state errors for navigation properties that are not bound from the form.
            // These errors can arise if the model binder attempts to validate these properties
            // even when they're not part of the input, especially if they're non-nullable
            // and backed by required foreign keys or collections.
            ModelState.Remove("CreatedBy");
            ModelState.Remove("Tasks");
            // Also explicitly remove CreatedByUserId as we are setting it manually
            ModelState.Remove("CreatedByUserId");


            // Get the current user's ID to set as the creator of the project
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                // If user ID is invalid, return unauthorized or redirect to login.
                TempData["ErrorMessage"] = "Authentication error: Could not identify user.";
                return RedirectToAction("Login", "Account");
            }

            // Manually set the CreatedByUserId BEFORE checking ModelState.IsValid
            // This ensures that the foreign key property, which might be implicitly required, has a value.
            project.CreatedByUserId = currentUserId;

            // Validate the model based on data annotations
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(project); // Add the new project to the database context
                    await _context.SaveChangesAsync(); // Save changes
                    TempData["SuccessMessage"] = $"Project '{project.ProjectName}' created successfully!";
                    return RedirectToAction(nameof(Index)); // Redirect to dashboard on success
                }
                catch (Exception ex)
                {
                    // Log the exception (e.g., using ILogger)
                    TempData["ErrorMessage"] = $"Error creating project: {ex.Message}";
                    // Re-populate ViewBag for project statuses before returning view
                    ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" }, project.ProjectStatus);
                    return View(project); // Stay on the create page with error
                }
            }
            // If ModelState is not valid, return the view with errors
            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" }, project.ProjectStatus);

            // --- DEBUGGING: Print validation errors to console ---
            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    Console.WriteLine($"Validation Error (CreateProject): {error.ErrorMessage}");
                }
            }
            // --- END DEBUGGING ---

            return View(project);
        }

        /// <summary>
        /// Displays the detailed view of a single task, including its comments.
        /// </summary>
        /// <param name="id">The ID of the task to display.</param>
        [HttpGet]
        public async Task<IActionResult> TaskDetails(int id)
        {
            // Fetch the specific task by ID, eagerly loading all related data:
            // Project, AssignedTo User, TaskCreatedBy User, and all TaskComments with their associated Users.
            var task = await _context.Tasks
                                     .Include(t => t.Project)
                                     .Include(t => t.AssignedTo)
                                     .Include(t => t.TaskCreatedBy)
                                     .Include(t => t.TaskComments)
                                        .ThenInclude(tc => tc.User) // This loads the User for each TaskComment
                                     .FirstOrDefaultAsync(m => m.Id == id);

            if (task == null)
            {
                return NotFound(); // Return 404 if task not found
            }

            // Map the Task model to the TaskViewModel for display.
            // This allows for including additional computed properties or flattening navigation properties.
            var taskViewModel = new TaskViewModel
            {
                Id = task.Id,
                TaskTitle = task.TaskTitle,
                TaskDescription = task.TaskDescription,
                ProjectId = task.ProjectId,
                Project = task.Project,
                AssignedToUserId = task.AssignedToUserId,
                AssignedTo = task.AssignedTo,
                TaskStatus = task.TaskStatus,
                TaskPriority = task.TaskPriority,
                TaskCreatedByUserId = task.TaskCreatedByUserId,
                TaskCreatedBy = task.TaskCreatedBy,
                TaskCreatedAt = task.TaskCreatedAt,
                TaskDueDate = task.TaskDueDate,
                TaskCompletedAt = task.TaskCompletedAt,
                Comments = task.TaskComments.OrderBy(c => c.CommentCreatedAt).ToList(), // Order comments by creation date
                AssignedToUsername = task.AssignedTo?.Username, // Null-conditional operator for safety
                ProjectName = task.Project?.ProjectName,
                CreatedByUsername = task.TaskCreatedBy?.Username
            };

            return View(taskViewModel);
        }

        /// <summary>
        /// Handles the submission for adding a new comment to a task.
        /// </summary>
        /// <param name="taskId">The ID of the task to add the comment to.</param>
        /// <param name="commentText">The text content of the comment.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int taskId, string commentText)
        {
            // Get the current user's ID
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                // If user ID is invalid, return unauthorized or redirect to login.
                return Unauthorized();
            }

            // Basic validation for comment text
            if (string.IsNullOrWhiteSpace(commentText))
            {
                // Add a model error and redirect back to the task details.
                // In a real app, you might want to display this error more gracefully.
                ModelState.AddModelError("commentText", "Comment cannot be empty.");
                return RedirectToAction(nameof(TaskDetails), new { id = taskId });
            }

            // Create a new TaskComment instance
            var taskComment = new TaskComment
            {
                TaskId = taskId,
                UserId = currentUserId,
                CommentText = commentText.Trim(), // Trim whitespace
                CommentCreatedAt = DateTime.UtcNow
            };

            _context.TaskComments.Add(taskComment); // Add the comment to the database context
            await _context.SaveChangesAsync(); // Save changes

            // Redirect back to the TaskDetails page for the same task
            return RedirectToAction(nameof(TaskDetails), new { id = taskId });
        }

        /// <summary>
        /// Displays the form for editing an existing task.
        /// </summary>
        /// <param name="id">The ID of the task to edit.</param>
        [HttpGet]
        public async Task<IActionResult> EditTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id); // Find the task by ID
            if (task == null)
            {
                return NotFound(); // Return 404 if task not found
            }

            // Populate dropdowns similar to CreateTask for Project and AssignedToUser
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", task.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", task.AssignedToUserId);

            // Populate dropdown for TaskStatus and TaskPriority (using consistent options)
            ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, task.TaskStatus); // Updated to match DB
            ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, task.TaskPriority);

            return View(task); // Pass the found task to the view
        }

        /// <summary>
        /// Handles the submission of the edited task form.
        /// </summary>
        /// <param name="id">The ID of the task being edited.</param>
        /// <param name="task">The Task model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTask(int id, [Bind("Id,TaskTitle,TaskDescription,ProjectId,AssignedToUserId,TaskStatus,TaskPriority,TaskDueDate,TaskCompletedAt")] StackFlow.Models.Task task)
        {
            if (id != task.Id)
            {
                return NotFound(); // Mismatch in ID, return 404
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(task); // Update the task in the database context
                    await _context.SaveChangesAsync(); // Save changes
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Check if the task actually exists before throwing error
                    if (!await _context.Tasks.AnyAsync(e => e.Id == task.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw; // Re-throw if it's a genuine concurrency issue
                    }
                }
                return RedirectToAction(nameof(Index)); // Redirect to dashboard on success
            }

            // If ModelState is not valid, re-populate ViewBags and return the view with errors
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", task.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", task.AssignedToUserId);
            ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, task.TaskStatus); // Updated to match DB
            ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, task.TaskPriority);

            return View(task); // Return the view with validation errors
        }

        /// <summary>
        /// Displays a report of projects with their task statistics.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProjectReports()
        {
            var projects = await _context.Projects
                                         .Include(p => p.Tasks) // Eager load tasks for each project
                                         .OrderBy(p => p.ProjectName)
                                         .ToListAsync();

            var projectReports = projects.Select(p => new ProjectReportViewModel
            {
                Project = p,
                TotalTasks = p.Tasks.Count,
                CompletedTasks = p.Tasks.Count(t => t.TaskStatus == "Done"), // Changed from "Completed" to "Done"
                InProgressTasks = p.Tasks.Count(t => t.TaskStatus == "In Progress"),
                ToDoTasks = p.Tasks.Count(t => t.TaskStatus == "To Do")
            }).ToList();

            return View(projectReports);
        }

        /// <summary>
        /// Displays a report of all users and their assigned task statistics.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UserReports()
        {
            // Fetch all users and include their assigned tasks
            var users = await _context.Users
                                      .Include(u => u.AssignedTasks) // Eager load tasks assigned to each user
                                      .OrderBy(u => u.Username)
                                      .ToListAsync();

            var userReports = users.Select(u => new UserReportViewModel
            {
                User = u,
                TotalTasksAssigned = u.AssignedTasks.Count,
                CompletedTasksAssigned = u.AssignedTasks.Count(t => t.TaskStatus == "Done"), // Changed from "Completed" to "Done"
                InProgressTasksAssigned = u.AssignedTasks.Count(t => t.TaskStatus == "In Progress"),
                ToDoTasksAssigned = u.AssignedTasks.Count(t => t.TaskStatus == "To Do"),
                AssignedTasks = u.AssignedTasks.Select(t => new TaskViewModel
                {
                    Id = t.Id,
                    TaskTitle = t.TaskTitle,
                    TaskStatus = t.TaskStatus,
                    TaskPriority = t.TaskPriority,
                    TaskDueDate = t.TaskDueDate
                }).ToList() // Populate a simplified list of assigned tasks
            }).ToList();

            return View(userReports);
        }
    }
}
```

The error messages `The Project field is required.`, `The AssignedTo field is required.`, `The TaskComments field is required.`, and `The TaskCreatedBy field is required.` indicate that the model binder is still trying to validate these navigation properties in your `Task` model during the `CreateTask` POST action, despite the `[Bind]` attribute. This can happen if the navigation properties themselves are implicitly required (e.g., if the foreign key for `Project` is `[Required]` and `ProjectId` is 0 or unassigned).

To address this, I will implement `ModelState.Remove()` for each of these navigation properties at the beginning of the `CreateTask` POST action. This tells the model state to ignore any validation errors for these specific properties, as they are not directly provided by the form but are handled by your database relationships and controller logic.

Here's the updated `DashboardController.cs` Canvas with these `ModelState.Remove()` calls for the `CreateTask` POST action:


```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Needed for SelectList
using Microsoft.EntityFrameworkCore;
using StackFlow.Data;
using StackFlow.Models;
using StackFlow.ViewModels;
using System; // Added for Exception
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic; // Added for List<string>

namespace StackFlow.Controllers
{
    // Ensures only authenticated users can access the dashboard
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Displays the main dashboard with an overview of tasks and projects for the current user.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Get the current user's ID from claims
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                // If user ID is not found or invalid, redirect to login.
                // This might indicate an authentication issue or corrupted cookie.
                return RedirectToAction("Login", "Account");
            }

            // Fetch the current user along with their role for display on the dashboard.
            var currentUser = await _context.Users
                                            .Include(u => u.Role) // Eager load the Role navigation property
                                            .FirstOrDefaultAsync(u => u.Id == currentUserId);

            if (currentUser == null)
            {
                // If the user is authenticated but not found in the database,
                // it suggests a data inconsistency or deleted user.
                return RedirectToAction("Login", "Account");
            }

            // Fetch all tasks, including their associated Project, AssignedTo User, and CreatedBy User.
            // This ensures all necessary data for task cards is available.
            var allTasks = await _context.Tasks
                                         .Include(t => t.Project)
                                         .Include(t => t.AssignedTo)
                                         .Include(t => t.TaskCreatedBy)
                                         .ToListAsync();

            // Fetch all projects. In a real application, you might filter this
            // based on user involvement (e.g., projects they created or are assigned to).
            var allProjects = await _context.Projects
                                            .Include(p => p.CreatedBy) // Include the user who created the project
                                            .ToListAsync();

            // Populate the DashboardViewModel with the fetched data.
            var viewModel = new DashboardViewModel
            {
                Username = currentUser.Username,
                Role = currentUser.Role?.Title ?? "Unknown Role", // Safely get role title, default if null
                CurrentUserId = currentUserId,
                // Filter tasks assigned to the current user
                AssignedToMeTasks = allTasks.Where(t => t.AssignedToUserId == currentUserId).ToList(),
                // Filter tasks by their status
                ToDoTasks = allTasks.Where(t => t.TaskStatus == "To Do").ToList(),
                InProgressTasks = allTasks.Where(t => t.TaskStatus == "In Progress").ToList(),
                CompletedTasks = allTasks.Where(t => t.TaskStatus == "Done").ToList(), // Changed from "Completed" to "Done"
                // Assign all projects (consider filtering for large datasets)
                Projects = allProjects
            };

            return View(viewModel);
        }

        /// <summary>
        /// Displays the form for creating a new task.
        /// Populates dropdowns for Project and AssignedToUser.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CreateTask()
        {
            // Fetch all projects to populate the Project dropdown
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName");

            // Fetch all users to populate the AssignedTo dropdown
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username");

            // Define consistent Task Statuses and Priorities
            ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" });
            ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" });


            // Initialize a new Task model to bind to the form
            return View(new StackFlow.Models.Task());
        }

        /// <summary>
        /// Handles the submission of the new task creation form.
        /// </summary>
        /// <param name="task">The Task model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTask([Bind("TaskTitle,TaskDescription,ProjectId,AssignedToUserId,TaskStatus,TaskPriority,TaskDueDate")] StackFlow.Models.Task task)
        {
            // IMPORTANT: Remove model state errors for navigation properties that are not bound from the form.
            // These errors can arise if the model binder attempts to validate these properties
            // even when they're not part of the input, especially if they're non-nullable
            // and backed by required foreign keys or collections.
            ModelState.Remove("Project");
            ModelState.Remove("AssignedTo");
            ModelState.Remove("TaskComments");
            ModelState.Remove("TaskCreatedBy"); // This is the navigation property
            ModelState.Remove("TaskCreatedByUserId"); // If this is also causing issues, remove it too


            // Get the current user's ID to set as the creator of the task
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                // If user ID is invalid, return unauthorized or redirect to login.
                TempData["ErrorMessage"] = "Authentication error: Could not identify user for task creation.";
                return RedirectToAction("Login", "Account");
            }

            // Manually set properties that are not part of the form binding
            task.TaskCreatedByUserId = currentUserId; // Set this BEFORE ModelState.IsValid check
            task.TaskCreatedAt = DateTime.UtcNow;

            // Validate the model based on data annotations
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(task); // Add the new task to the database context
                    await _context.SaveChangesAsync(); // Save changes
                    TempData["SuccessMessage"] = $"Task '{task.TaskTitle}' created successfully!";
                    return RedirectToAction(nameof(Index)); // Redirect to dashboard on success
                }
                catch (Exception ex)
                {
                    // Log the exception (e.g., using ILogger)
                    TempData["ErrorMessage"] = $"Error creating task: {ex.Message}";
                    // Re-populate ViewBags before returning the view
                    ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", task.ProjectId);
                    ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", task.AssignedToUserId);
                    ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, task.TaskStatus);
                    ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, task.TaskPriority);
                    return View(task); // Stay on the create page with error
                }
            }

            // If ModelState is not valid, re-populate ViewBag and return the view with errors
            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", task.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", task.AssignedToUserId);
            ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, task.TaskStatus);
            ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, task.TaskPriority);

            // --- DEBUGGING: Print validation errors to console ---
            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    Console.WriteLine($"Validation Error (CreateTask): {error.ErrorMessage}");
                }
            }
            // --- END DEBUGGING ---

            return View(task); // Return the view with validation errors
        }

        /// <summary>
        /// Displays the form for creating a new project.
        /// </summary>
        [HttpGet]
        public IActionResult CreateProject()
        {
            // Define consistent Project Statuses based on DB CHECK constraint
            ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" });
            return View(new Project()); // Return a new Project model
        }

        /// <summary>
        /// Handles the submission of the new project creation form.
        /// </summary>
        /// <param name="project">The Project model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Updated [Bind] attribute to exclude navigation properties 'CreatedBy' and 'Tasks'
        public async Task<IActionResult> CreateProject([Bind("ProjectName,ProjectDescription,ProjectStartDate,ProjectEndDate,ProjectStatus")] Project project)
        {
            // IMPORTANT: Remove model state errors for navigation properties that are not bound from the form.
            // These errors can arise if the model binder attempts to validate these properties
            // even when they're not part of the input, especially if they're non-nullable
            // and backed by required foreign keys or collections.
            ModelState.Remove("CreatedBy");
            ModelState.Remove("Tasks");
            // Also explicitly remove CreatedByUserId as we are setting it manually
            ModelState.Remove("CreatedByUserId");


            // Get the current user's ID to set as the creator of the project
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                // If user ID is invalid, return unauthorized or redirect to login.
                TempData["ErrorMessage"] = "Authentication error: Could not identify user.";
                return RedirectToAction("Login", "Account");
            }

            // Manually set the CreatedByUserId BEFORE checking ModelState.IsValid
            // This ensures that the foreign key property, which might be implicitly required, has a value.
            project.CreatedByUserId = currentUserId;

            // Validate the model based on data annotations
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(project); // Add the new project to the database context
                    await _context.SaveChangesAsync(); // Save changes
                    TempData["SuccessMessage"] = $"Project '{project.ProjectName}' created successfully!";
                    return RedirectToAction(nameof(Index)); // Redirect to dashboard on success
                }
                catch (Exception ex)
                {
                    // Log the exception (e.g., using ILogger)
                    TempData["ErrorMessage"] = $"Error creating project: {ex.Message}";
                    // Re-populate ViewBag for project statuses before returning view
                    ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" }, project.ProjectStatus);
                    return View(project); // Stay on the create page with error
                }
            }
            // If ModelState is not valid, return the view with errors
            TempData["ErrorMessage"] = "Please correct the errors in the form.";
            ViewBag.ProjectStatuses = new SelectList(new List<string> { "Active", "Completed", "On Hold" }, project.ProjectStatus);

            // --- DEBUGGING: Print validation errors to console ---
            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    Console.WriteLine($"Validation Error (CreateProject): {error.ErrorMessage}");
                }
            }
            // --- END DEBUGGING ---

            return View(project);
        }

        /// <summary>
        /// Displays the detailed view of a single task, including its comments.
        /// </summary>
        /// <param name="id">The ID of the task to display.</param>
        [HttpGet]
        public async Task<IActionResult> TaskDetails(int id)
        {
            // Fetch the specific task by ID, eagerly loading all related data:
            // Project, AssignedTo User, TaskCreatedBy User, and all TaskComments with their associated Users.
            var task = await _context.Tasks
                                     .Include(t => t.Project)
                                     .Include(t => t.AssignedTo)
                                     .Include(t => t.TaskCreatedBy)
                                     .Include(t => t.TaskComments)
                                        .ThenInclude(tc => tc.User) // This loads the User for each TaskComment
                                     .FirstOrDefaultAsync(m => m.Id == id);

            if (task == null)
            {
                return NotFound(); // Return 404 if task not found
            }

            // Map the Task model to the TaskViewModel for display.
            // This allows for including additional computed properties or flattening navigation properties.
            var taskViewModel = new TaskViewModel
            {
                Id = task.Id,
                TaskTitle = task.TaskTitle,
                TaskDescription = task.TaskDescription,
                ProjectId = task.ProjectId,
                Project = task.Project,
                AssignedToUserId = task.AssignedToUserId,
                AssignedTo = task.AssignedTo,
                TaskStatus = task.TaskStatus,
                TaskPriority = task.TaskPriority,
                TaskCreatedByUserId = task.TaskCreatedByUserId,
                TaskCreatedBy = task.TaskCreatedBy,
                TaskCreatedAt = task.TaskCreatedAt,
                TaskDueDate = task.TaskDueDate,
                TaskCompletedAt = task.TaskCompletedAt,
                Comments = task.TaskComments.OrderBy(c => c.CommentCreatedAt).ToList(), // Order comments by creation date
                AssignedToUsername = task.AssignedTo?.Username, // Null-conditional operator for safety
                ProjectName = task.Project?.ProjectName,
                CreatedByUsername = task.TaskCreatedBy?.Username
            };

            return View(taskViewModel);
        }

        /// <summary>
        /// Handles the submission for adding a new comment to a task.
        /// </summary>
        /// <param name="taskId">The ID of the task to add the comment to.</param>
        /// <param name="commentText">The text content of the comment.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int taskId, string commentText)
        {
            // Get the current user's ID
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId))
            {
                // If user ID is invalid, return unauthorized or redirect to login.
                return Unauthorized();
            }

            // Basic validation for comment text
            if (string.IsNullOrWhiteSpace(commentText))
            {
                // Add a model error and redirect back to the task details.
                // In a real app, you might want to display this error more gracefully.
                ModelState.AddModelError("commentText", "Comment cannot be empty.");
                return RedirectToAction(nameof(TaskDetails), new { id = taskId });
            }

            // Create a new TaskComment instance
            var taskComment = new TaskComment
            {
                TaskId = taskId,
                UserId = currentUserId,
                CommentText = commentText.Trim(), // Trim whitespace
                CommentCreatedAt = DateTime.UtcNow
            };

            _context.TaskComments.Add(taskComment); // Add the comment to the database context
            await _context.SaveChangesAsync(); // Save changes

            // Redirect back to the TaskDetails page for the same task
            return RedirectToAction(nameof(TaskDetails), new { id = taskId });
        }

        /// <summary>
        /// Displays the form for editing an existing task.
        /// </summary>
        /// <param name="id">The ID of the task to edit.</param>
        [HttpGet]
        public async Task<IActionResult> EditTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id); // Find the task by ID
            if (task == null)
            {
                return NotFound(); // Return 404 if task not found
            }

            // Populate dropdowns similar to CreateTask for Project and AssignedToUser
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", task.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", task.AssignedToUserId);

            // Populate dropdown for TaskStatus and TaskPriority (using consistent options)
            ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, task.TaskStatus); // Updated to match DB
            ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, task.TaskPriority);

            return View(task); // Pass the found task to the view
        }

        /// <summary>
        /// Handles the submission of the edited task form.
        /// </summary>
        /// <param name="id">The ID of the task being edited.</param>
        /// <param name="task">The Task model populated from the form.</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTask(int id, [Bind("Id,TaskTitle,TaskDescription,ProjectId,AssignedToUserId,TaskStatus,TaskPriority,TaskDueDate,TaskCompletedAt")] StackFlow.Models.Task task)
        {
            if (id != task.Id)
            {
                return NotFound(); // Mismatch in ID, return 404
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(task); // Update the task in the database context
                    await _context.SaveChangesAsync(); // Save changes
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Check if the task actually exists before throwing error
                    if (!await _context.Tasks.AnyAsync(e => e.Id == task.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw; // Re-throw if it's a genuine concurrency issue
                    }
                }
                return RedirectToAction(nameof(Index)); // Redirect to dashboard on success
            }

            // If ModelState is not valid, re-populate ViewBags and return the view with errors
            ViewBag.Projects = new SelectList(await _context.Projects.ToListAsync(), "Id", "ProjectName", task.ProjectId);
            ViewBag.Users = new SelectList(await _context.Users.ToListAsync(), "Id", "Username", task.AssignedToUserId);
            ViewBag.TaskStatuses = new SelectList(new List<string> { "To Do", "In Progress", "In Review", "Done" }, task.TaskStatus); // Updated to match DB
            ViewBag.TaskPriorities = new SelectList(new List<string> { "Low", "Medium", "High" }, task.TaskPriority);

            return View(task); // Return the view with validation errors
        }

        /// <summary>
        /// Displays a report of projects with their task statistics.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProjectReports()
        {
            var projects = await _context.Projects
                                         .Include(p => p.Tasks) // Eager load tasks for each project
                                         .OrderBy(p => p.ProjectName)
                                         .ToListAsync();

            var projectReports = projects.Select(p => new ProjectReportViewModel
            {
                Project = p,
                TotalTasks = p.Tasks.Count,
                CompletedTasks = p.Tasks.Count(t => t.TaskStatus == "Done"), // Changed from "Completed" to "Done"
                InProgressTasks = p.Tasks.Count(t => t.TaskStatus == "In Progress"),
                ToDoTasks = p.Tasks.Count(t => t.TaskStatus == "To Do")
            }).ToList();

            return View(projectReports);
        }

        /// <summary>
        /// Displays a report of all users and their assigned task statistics.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> UserReports()
        {
            // Fetch all users and include their assigned tasks
            var users = await _context.Users
                                      .Include(u => u.AssignedTasks) // Eager load tasks assigned to each user
                                      .OrderBy(u => u.Username)
                                      .ToListAsync();

            var userReports = users.Select(u => new UserReportViewModel
            {
                User = u,
                TotalTasksAssigned = u.AssignedTasks.Count,
                CompletedTasksAssigned = u.AssignedTasks.Count(t => t.TaskStatus == "Done"), // Changed from "Completed" to "Done"
                InProgressTasksAssigned = u.AssignedTasks.Count(t => t.TaskStatus == "In Progress"),
                ToDoTasksAssigned = u.AssignedTasks.Count(t => t.TaskStatus == "To Do"),
                AssignedTasks = u.AssignedTasks.Select(t => new TaskViewModel
                {
                    Id = t.Id,
                    TaskTitle = t.TaskTitle,
                    TaskStatus = t.TaskStatus,
                    TaskPriority = t.TaskPriority,
                    TaskDueDate = t.TaskDueDate
                }).ToList() // Populate a simplified list of assigned tasks
            }).ToList();

            return View(userReports);
        }
    }
}
