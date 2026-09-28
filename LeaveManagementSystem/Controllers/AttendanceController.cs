using System.Security.Claims;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Services.Interfaces;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagementSystem.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private readonly IAttendanceService _attendance;
    private readonly IWorkingCalendarService _calendar;
    private readonly ILeaveService _leaves;
    private readonly IEmployeeService _employees;

    public AttendanceController(
        IAttendanceService attendance, IWorkingCalendarService calendar,
        ILeaveService leaves, IEmployeeService employees)
    {
        _attendance = attendance;
        _calendar = calendar;
        _leaves = leaves;
        _employees = employees;
    }

    private int CurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ---- Employee ----

    [HttpGet]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> Today()
    {
        var userId = CurrentUserId();
        var today = DateTime.Today;
        return View(new AttendanceTodayViewModel
        {
            Today = await _attendance.GetTodayAsync(userId),
            IsWeekend = _calendar.IsWeekend(today),
            IsHoliday = await _calendar.IsHolidayAsync(today)
        });
    }

    [HttpPost]
    [Authorize(Roles = "Employee")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn()
    {
        var result = await _attendance.CheckInAsync(CurrentUserId());
        TempData[result.Success ? "Message" : "Error"] = result.Success
            ? "Checked in — marked Present for today."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Today));
    }

    [HttpPost]
    [Authorize(Roles = "Employee")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut()
    {
        var result = await _attendance.CheckOutAsync(CurrentUserId());
        TempData[result.Success ? "Message" : "Error"] = result.Success
            ? "Checked out for today."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Today));
    }

    [HttpGet]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> History()
    {
        return View(new AttendanceHistoryViewModel
        {
            Records = await _attendance.GetHistoryAsync(CurrentUserId())
        });
    }

    [HttpGet]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> Month(int? year, int? month)
    {
        var y = Math.Clamp(year ?? DateTime.Today.Year, 2000, 2100);
        var m = Math.Clamp(month ?? DateTime.Today.Month, 1, 12);
        var records = await _attendance.GetByMonthAsync(CurrentUserId(), y, m);
        var from = new DateTime(y, m, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var today = DateTime.Today;
        var holidays = await _calendar.GetHolidaysInRangeAsync(from, to);
        // Phase 10 — approved/pending leave days render as Leave, never Absent.
        var history = await _leaves.GetHistoryAsync(CurrentUserId());
        var leaveByDate = new Dictionary<DateTime, string>();
        foreach (var l in history.Where(l => l.Status != LeaveStatus.Rejected))
        {
            var label = l.Status == LeaveStatus.Approved ? "Leave" : "Pending";
            for (var d = l.FromDate.Date; d <= l.ToDate.Date; d = d.AddDays(1))
            {
                if (d < from || d > to)
                {
                    continue;
                }

                // Approved leave wins over pending on the same day.
                if (!leaveByDate.TryGetValue(d, out var cur) || label == "Leave")
                {
                    leaveByDate[d] = label;
                }
            }
        }
        var canMark = from <= today && today <= to
            && !_calendar.IsWeekend(today)
            && !holidays.Contains(today)
            && !records.Any(r => r.Date.Date == today);
        return View(new AttendanceMonthViewModel
        {
            Year = y,
            Month = m,
            Records = records,
            ByDate = records.ToDictionary(r => r.Date.Date, r => r),
            Holidays = holidays,
            LeaveByDate = leaveByDate,
            CanMarkToday = canMark,
            SubmitForm = new SubmitAttendanceViewModel { Date = today }
        });
    }

    // Phase 9 — employee self-submission from the calendar popup.
    // The date field is read-only in the UI and re-validated here: it must
    // equal today. Past/future, weekend, holiday and duplicate submissions
    // are rejected by the service (backend is the source of truth).
    [HttpPost]
    [Authorize(Roles = "Employee")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(SubmitAttendanceViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Month));
        }

        var result = await _attendance.SubmitDailyAttendanceAsync(
            CurrentUserId(), model.Date, model.WorkingHours!.Value,
            model.ProjectName!, model.Remarks);
        TempData[result.Success ? "Message" : "Error"] = result.Success
            ? $"Attendance submitted for {result.Data!.Date:yyyy-MM-dd} — Present, {result.Data.WorkingHours:0.##} hours, Project: {result.Data.ProjectName}."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Month));
    }

    // ---- Admin ----

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Daily(DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        return View(new AdminDailyAttendanceViewModel
        {
            Date = day,
            Records = await _attendance.GetDailyAsync(day)
        });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Monthly(int? year, int? month)
    {
        var y = Math.Clamp(year ?? DateTime.Today.Year, 2000, 2100);
        var m = Math.Clamp(month ?? DateTime.Today.Month, 1, 12);
        return View(new AdminMonthlyAttendanceViewModel
        {
            Year = y,
            Month = m,
            Records = await _attendance.GetMonthlyAsync(y, m)
        });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EmployeeWise(int? year, int? month, string? search)
    {
        var y = Math.Clamp(year ?? DateTime.Today.Year, 2000, 2100);
        var m = Math.Clamp(month ?? DateTime.Today.Month, 1, 12);
        return View(new AdminEmployeeWiseViewModel
        {
            Year = y,
            Month = m,
            Search = search,
            Rows = await _attendance.GetEmployeeWiseAsync(y, m, search)
        });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Correct(int id)
    {
        var record = await _attendance.GetByIdAsync(id);
        if (record is null)
        {
            return NotFound();
        }

        return View(new AttendanceCorrectionViewModel
        {
            RecordId = record.Id,
            UserId = record.UserId,
            EmployeeName = record.User?.FullName ?? $"User #{record.UserId}",
            Date = record.Date,
            Status = record.Status,
            WorkingHours = record.WorkingHours,
            ProjectName = record.ProjectName,
            Remarks = record.Remarks
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Correct(AttendanceCorrectionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _attendance.CorrectAsync(
            model.RecordId, model.Status, model.Remarks, model.WorkingHours, model.ProjectName,
            CurrentUserId());
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update the record.");
            return View(model);
        }

        TempData["Message"] = "Attendance record updated.";
        return RedirectToAction(nameof(Daily), new { date = result.Data!.Date.ToString("yyyy-MM-dd") });
    }

    // Phase 11 — Admin backdate entry: Employee + Date selector. The employee
    // date restriction (current day only) does NOT apply here; this action is
    // Admin-only by role, which is what makes the bypass secure. Any past
    // working date may be filled; weekends/holidays/future are still refused
    // by the service so they keep rendering as Weekend/Holiday.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Mark()
    {
        ViewBag.Employees = await _employees.GetEmployeesAsync(null);
        return View(new AttendanceUpsertViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Mark(AttendanceUpsertViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Employees = await _employees.GetEmployeesAsync(null);
            return View(model);
        }

        var result = await _attendance.UpsertAsync(
            model.UserId, model.Date, model.Status, model.Remarks,
            model.WorkingHours, model.ProjectName, CurrentUserId());
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not save the record.");
            ViewBag.Employees = await _employees.GetEmployeesAsync(null);
            return View(model);
        }

        TempData["Message"] = "Attendance saved.";
        return RedirectToAction(nameof(Daily), new { date = result.Data!.Date.ToString("yyyy-MM-dd") });
    }

    // Phase 9 — centralized admin report: every attendance record in the
    // month (status, hours, project) plus per-employee totals, so the admin
    // can see who worked on which project and for how many hours.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Report(int? year, int? month, string? search)
    {
        var y = Math.Clamp(year ?? DateTime.Today.Year, 2000, 2100);
        var m = Math.Clamp(month ?? DateTime.Today.Month, 1, 12);
        var rows = await _attendance.GetEmployeeWiseAsync(y, m, search);
        var records = rows.SelectMany(r => r.Records).OrderBy(r => r.Date).ToList();
        return View(new AdminAttendanceReportViewModel
        {
            Year = y,
            Month = m,
            Search = search,
            Records = records,
            Totals = rows.Select(r => new AdminAttendanceReportRow
            {
                UserId = r.User.Id,
                EmployeeName = r.User.FullName,
                Email = r.User.Email,
                DaysPresent = r.Records.Count(a => a.Status == AttendanceStatus.Present),
                TotalHours = r.Records.Where(a => a.WorkingHours.HasValue).Sum(a => a.WorkingHours!.Value),
                Projects = r.Records
                    .Where(a => !string.IsNullOrWhiteSpace(a.ProjectName))
                    .Select(a => a.ProjectName!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p)
                    .ToList()
            }).ToList()
        });
    }
}
