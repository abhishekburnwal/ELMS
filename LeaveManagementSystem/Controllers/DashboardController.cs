using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagementSystem.Controllers;

// Single post-login entry point — redirects by role (ARCHITECTURE.md §3).
[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Index", "Admin");
        }

        if (User.IsInRole("Employee"))
        {
            return RedirectToAction("Dashboard", "Employee");
        }

        return RedirectToAction("Login", "Account");
    }
}
