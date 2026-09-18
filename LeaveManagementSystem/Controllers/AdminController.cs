using System.Security.Claims;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services;
using LeaveManagementSystem.Services.Interfaces;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagementSystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IEmployeeService _employees;
    private readonly ILeaveRepository _leaves;
    private readonly ILeaveService _leaveService;

    public AdminController(IEmployeeService employees, ILeaveRepository leaves, ILeaveService leaveService)
    {
        _employees = employees;
        _leaves = leaves;
        _leaveService = leaveService;
    }

    // ELMS-12 — counts computed on read from the underlying data.
    public async Task<IActionResult> Index()
    {
        var employees = await _employees.GetEmployeesAsync(null);
        var all = await _leaveService.GetAllAsync();

        return View(new AdminDashboardViewModel
        {
            TotalEmployees = employees.Count,
            PendingCount = all.Count(l => l.Status == LeaveStatus.Pending),
            ApprovedCount = all.Count(l => l.Status == LeaveStatus.Approved),
            RejectedCount = all.Count(l => l.Status == LeaveStatus.Rejected)
        });
    }

    // ELMS-06 — list + name/email search, with used/remaining per DATABASE.md §6.
    [HttpGet]
    public async Task<IActionResult> Employees(string? search)
    {
        var users = await _employees.GetEmployeesAsync(search);
        var approved = await _leaves.GetApprovedForUsersAsync(users.Select(u => u.Id));
        var usedByUser = approved
            .GroupBy(l => l.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(l => LeaveDaysCalculator.CountWorkingDays(l.FromDate, l.ToDate)));

        var model = users.Select(u => new EmployeeListItemViewModel
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            IsActive = u.IsActive,
            LeaveBalance = u.LeaveBalance,
            UsedDays = usedByUser.TryGetValue(u.Id, out var used) ? used : 0,
            RemainingDays = u.LeaveBalance - (usedByUser.TryGetValue(u.Id, out var usedDays) ? usedDays : 0)
        }).ToList();

        ViewData["Search"] = search;
        return View(model);
    }

    // ELMS-07 — create.
    [HttpGet]
    public IActionResult CreateEmployee() => View("EmployeeForm", new EmployeeFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(EmployeeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("EmployeeForm", model);
        }

        var result = await _employees.CreateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.ErrorMessage ?? "Could not create the employee.");
            return View("EmployeeForm", model);
        }

        return RedirectToAction(nameof(Employees));
    }

    // ELMS-07 — edit.
    [HttpGet]
    public async Task<IActionResult> EditEmployee(int id)
    {
        var user = await _employees.GetByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        return View("EmployeeForm", new EmployeeFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            LeaveBalance = user.LeaveBalance
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEmployee(EmployeeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("EmployeeForm", model);
        }

        var result = await _employees.UpdateAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.ErrorMessage ?? "Could not update the employee.");
            return View("EmployeeForm", model);
        }

        return RedirectToAction(nameof(Employees));
    }

    // ELMS-08 — soft delete (IsActive toggle, never a hard delete).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateEmployee(int id)
    {
        await _employees.DeactivateAsync(id);
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateEmployee(int id)
    {
        await _employees.ReactivateAsync(id);
        return RedirectToAction(nameof(Employees));
    }

    // ELMS-11 + ELMS-18 — all requests, filterable by status/date/employee.
    [HttpGet]
    public async Task<IActionResult> LeaveRequests(
        string? status, DateTime? from, DateTime? to, string? search)
    {
        LeaveStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, ignoreCase: true, out var parsed))
        {
            filter = parsed;
        }

        ViewData["StatusFilter"] = filter?.ToString();
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        ViewData["Search"] = search;
        return View(await _leaveService.GetFilteredAsync(filter, from, to, search));
    }

    // ELMS-18 — export the current filter to Excel, streamed in the response.
    [HttpGet]
    public async Task<IActionResult> ExportLeaveRequests(
        string? status, DateTime? from, DateTime? to, string? search)
    {
        LeaveStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, ignoreCase: true, out var parsed))
        {
            filter = parsed;
        }

        var rows = await _leaveService.GetFilteredAsync(filter, from, to, search);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Leave Requests");
        string[] headers =
        {
            "Employee", "Email", "From", "To", "Days", "Reason",
            "Status", "Applied", "Reviewed By", "Reviewed Date", "Remarks"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var r in rows)
        {
            sheet.Cell(row, 1).Value = r.User?.FullName ?? string.Empty;
            sheet.Cell(row, 2).Value = r.User?.Email ?? string.Empty;
            sheet.Cell(row, 3).Value = r.FromDate;
            sheet.Cell(row, 3).Style.DateFormat.Format = "yyyy-MM-dd";
            sheet.Cell(row, 4).Value = r.ToDate;
            sheet.Cell(row, 4).Style.DateFormat.Format = "yyyy-MM-dd";
            sheet.Cell(row, 5).Value = LeaveDaysCalculator.CountWorkingDays(r.FromDate, r.ToDate);
            sheet.Cell(row, 6).Value = r.Reason;
            sheet.Cell(row, 7).Value = r.Status.ToString();
            sheet.Cell(row, 8).Value = r.AppliedDate;
            sheet.Cell(row, 8).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
            sheet.Cell(row, 9).Value = r.ReviewedBy?.FullName ?? string.Empty;
            sheet.Cell(row, 10).Value = r.ReviewedDate;
            sheet.Cell(row, 10).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
            sheet.Cell(row, 11).Value = r.Remarks ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"LeaveRequests-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? remarks) =>
        await Review(id, LeaveStatus.Approved, remarks);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? remarks) =>
        await Review(id, LeaveStatus.Rejected, remarks);

    private async Task<IActionResult> Review(int id, LeaveStatus decision, string? remarks)
    {
        var reviewerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _leaveService.UpdateStatusAsync(id, decision, reviewerId, remarks);

        if (result.Success)
        {
            TempData["Message"] = $"Request #{id} has been {decision.ToString().ToLower()}.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage;
        }

        return RedirectToAction(nameof(LeaveRequests));
    }
}
