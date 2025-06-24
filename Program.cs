using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient; // Keeping this from your provided code, though not explicitly used for connection string parsing here
using Microsoft.EntityFrameworkCore;
using StackFlow.Data;
using StackFlow.Hubs; // Add this using directive for your SignalR Hub

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure DbContext with SQL Server, retry logic, and command timeout
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        });
});

// Configure authentication using Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        // Configure cookie expiration to 1 hour
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });
// Add SignalR services
builder.Services.AddSignalR();


    var app = builder.Build();

    // Apply migrations automatically at startup, with basic error handling
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Database migration failed: " + ex.Message);
            // Optionally log this or notify via monitoring tool
        }
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    // Map SignalR Hubs
    app.MapHub<DashboardHub>("/dashboardHub"); // Maps the DashboardHub to the URL /dashboardHub

    // Map Controller routes (keeping your provided default route)
    // The default route is now Dashboard/Index to land authenticated users directly on the dashboard.
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}"); // Changed default controller to Dashboard and action to Index

    app.Run();
