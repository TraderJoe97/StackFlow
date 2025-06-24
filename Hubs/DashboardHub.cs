using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace StackFlow.Hubs
{
    /// <summary>
    /// SignalR Hub for broadcasting real-time updates related to dashboard data,
    /// including tasks and projects.
    /// </summary>
    public class DashboardHub : Hub
    {
        // This method can be called by clients to send a message to the hub,
        // which then broadcasts it to all connected clients.
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        // Methods for broadcasting specific entity updates (server-side calls)
        // These will be called from your controllers.

        /// <summary>
        /// Sends a notification that tasks have been updated (e.g., new task, task edited, status changed).
        /// Clients will listen for "ReceiveTaskUpdate".
        /// </summary>
        /// <param name="message">A message describing the update (e.g., "Task 'X' created").</param>
        public async Task SendTaskUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveTaskUpdate", message);
        }

        /// <summary>
        /// Sends a notification that projects have been updated.
        /// Clients will listen for "ReceiveProjectUpdate".
        /// </summary>
        /// <param name="message">A message describing the update (e.g., "Project 'Y' updated").</param>
        public async Task SendProjectUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveProjectUpdate", message);
        }

        /// <summary>
        /// Sends a notification that a user's role has been updated.
        /// Clients will listen for "ReceiveUserRoleUpdate".
        /// </summary>
        /// <param name="userId">The ID of the user whose role was updated.</param>
        /// <param name="newRole">The new role title.</param>
        /// <param name="message">A message describing the update.</param>
        public async Task SendUserRoleUpdate(int userId, string newRole, string message)
        {
            await Clients.All.SendAsync("ReceiveUserRoleUpdate", userId, newRole, message);
        }

        // You can add more specific methods here as needed,
        // for example, sending specific task objects or project objects.
    }
}
