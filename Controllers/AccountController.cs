using Microsoft.AspNetCore.Mvc;
using StackFlow.Data;
using StackFlow.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BCrypt.Net; // For password hashing

namespace StackFlow.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewData["LoginError"] = "Email and password are required.";
                return View();
            }

            // Find user by email
            // Include Role to get the role title for claims
            var user = await _context.Users
                                     .Include(u => u.Role)
                                     .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ViewData["LoginError"] = "Invalid email or password.";
                return View();
            }

            // Authentication successful, create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Unique identifier for the user
                new Claim(ClaimTypes.Name, user.Username), // Primary name for the user
                new Claim(ClaimTypes.Email, user.Email) // User's email
            };

            // Add role claim if user has a role
            if (user.Role != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role.Title)); // Add the role title as a role claim
            }

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Keep session alive across browser restarts
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) // Set session expiration
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Redirect to dashboard or returnUrl
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Dashboard"); // Default dashboard
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewData["RegistrationError"] = "All fields are required.";
                return View();
            }

            // Basic email format validation
            if (!email.EndsWith("@omnitak.com"))
            {
                ViewData["RegistrationError"] = "Only @omnitak.com email addresses are allowed.";
                return View();
            }

            // Check if user with this email or username already exists
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ViewData["RegistrationError"] = "Email already registered.";
                return View();
            }
            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                ViewData["RegistrationError"] = "Username already taken.";
                return View();
            }

            // Hash password
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            // Assign default role (e.g., 'Developer')
            var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.Title == "Developer");
            if (defaultRole == null)
            {
                // Fallback if 'Developer' role isn't seeded, or create it if not found
                // For a robust system, ensure roles are seeded during app startup/migration
                ViewData["RegistrationError"] = "Default role 'Developer' not found. Please contact support.";
                return View();
            }

            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                RoleId = defaultRole.Id, // Assign the ID of the 'Developer' role
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Registration successful! You can now log in.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
