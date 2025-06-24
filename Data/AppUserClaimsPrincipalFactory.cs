using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // Needed for Include() and accessing AppDbContext
using StackFlow.Models; // Your custom User and Role models
using StackFlow.Data; // Your AppDbContext

namespace StackFlow.Data
{
    /// <summary>
    /// Custom ClaimsPrincipalFactory to ensure that the user's role from the custom
    /// Role table is added as a ClaimTypes.Role during authentication.
    /// This allows [Authorize(Roles = "RoleName")] attributes to work correctly.
    /// </summary>
    public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User, Role>
    {
        private readonly AppDbContext _dbContext; // Inject your DbContext

        public AppUserClaimsPrincipalFactory(
            UserManager<User> userManager, // Use your custom User model here
            RoleManager<Role> roleManager, // Use your custom Role model here
            IOptions<IdentityOptions> optionsAccessor,
            AppDbContext dbContext) // Inject AppDbContext
            : base(userManager, roleManager, optionsAccessor)
        {
            _dbContext = dbContext;
        }

        public override async Task<ClaimsPrincipal> CreateAsync(User user)
        {
            // Call the base method to get the default ClaimsPrincipal (includes username, etc.)
            var principal = await base.CreateAsync(user);
            var identity = (ClaimsIdentity)principal.Identity;

            // Ensure the user object has the Role navigation property loaded
            // We need the Role's Title to add it as a claim
            var userWithRole = await _dbContext.Users
                                               .Include(u => u.Role) // Eager load the Role
                                               .FirstOrDefaultAsync(u => u.Id == user.Id);

            if (userWithRole?.Role != null)
            {
                // Add the custom role's Title as a ClaimTypes.Role
                // This is critical for [Authorize(Roles = "Admin")] to work
                identity.AddClaim(new Claim(ClaimTypes.Role, userWithRole.Role.Title));
            }

            return principal;
        }
    }
}
