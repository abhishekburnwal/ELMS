using System.Security.Claims;
using LeaveManagementSystem.Services.Interfaces;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagementSystem.Controllers;

// Common Employee Calendar: Present, Absent, Pending/Approved/Rejected Leave,
// Holiday, Weekend — merged from existing leave + attendance records.
// Employees see their own calendar; Admins pick an employee + month/year and
// can click any date for hours/project details. Leave/Holiday/Weekend
// integration is unchanged (EmployeeCalendarService precedence).
[Authorize]
public class CalendarController : Controller
{
    private readonly IEmployeeCalendarService _calendars;
    private readonly IEmployeeService _employees;
    private readonly IAttendanceService _attendance;

    public CalendarController(
        IEmployeeCalendarService calendars,
        IEmployeeService employees,
        IAttendanceService attendance)
    {
        _calendars = calendars;
        _employees = employees;
        _attendance = attendance;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month, int? userId)
    {
        var y = Math.Clamp(year ?? DateTime.Today.Year, 2000, 2100);
        var m = Math.Clamp(month ?? DateTime.Today.Month, 1, 12);

        int targetUserId;
        if (User.IsInRole("Admin"))
        {
            ViewData["Employees"] = await _employees.GetEmployeesAsync(null);
            if (userId.HasValue)
            {
                var target = await _employees.GetByIdAsync(userId.Value);
                if (target is null)
                {
                    return NotFound();
                }

                targetUserId = target.Id;
                ViewData["EmployeeName"] = target.FullName;
            }
            else
            {
                // Default to the first employee so the admin calendar always
                // shows one employee-wise calendar; the selector can change it.
                var first = (await _employees.GetEmployeesAsync(null)).FirstOrDefault();
                if (first is null)
                {
                    return View(new EmployeeCalendarViewModel
                    {
                        Year = y,
                        Month = m,
                        Employees = new List<Models.Entities.User>()
                    });
                }

                targetUserId = first.Id;
                ViewData["EmployeeName"] = first.FullName;
            }
        }
        else
        {
            targetUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        var days = await _calendars.BuildMonthAsync(targetUserId, y, m);
        var attendance = await _attendance.GetByMonthAsync(targetUserId, y, m);
        var summary = days.GroupBy(d => d.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return View(new EmployeeCalendarViewModel
        {
            Year = y,
            Month = m,
            Days = days,
            Summary = summary,
            SelectedUserId = targetUserId,
            Employees = User.IsInRole("Admin")
                ? await _employees.GetEmployeesAsync(null)
                : new List<Models.Entities.User>(),
            AttendanceByDate = attendance.ToDictionary(a => a.Date.Date, a => a)
        });
    }
}
