using LeaveManagementSystem.Data;
using LeaveManagementSystem.Hubs;
using LeaveManagementSystem.Middleware;
using LeaveManagementSystem.Repositories;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services;
using LeaveManagementSystem.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database — SQL Server Express only (ENVIRONMENT.md).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cookie authentication with role claims (ARCHITECTURE.md §3).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddControllersWithViews();

// Real-time push for leave decisions (ELMS-17, ARCHITECTURE.md §7).
builder.Services.AddSignalR();

// Service + repository wiring (ARCHITECTURE.md §8) — all Scoped.
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILeaveRepository, LeaveRepository>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IExceptionLogStore, ExceptionLogStore>();

var app = builder.Build();

// Global exception handling (ARCHITECTURE.md §6).
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

// ELMS-21 — centralized exception logging, INSIDE UseExceptionHandler on
// purpose: the handler middleware must stay outermost to render /Home/Error,
// so this catches first, records the details, and rethrows to it. The generic
// error page (and its no-sensitive-details guarantee) is unchanged.
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<NotificationHub>("/hub/notifications");

// Idempotent seed guard for the two default accounts (DATABASE.md §4).
try
{
    SeedData.Initialize(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
    logger.LogError(ex, "Seed data initialization failed. The app will still start.");
}

app.Run();
