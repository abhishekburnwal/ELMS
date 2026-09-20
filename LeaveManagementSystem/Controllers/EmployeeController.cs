using System.Security.Claims;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Services.Interfaces;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagementSystem.Controllers;

[Authorize(Roles = "Employee")]
public class EmployeeController : Controller
{
    private readonly ILeaveService _leaves;
    private readonly IEmployeeService _employees;

    public EmployeeController(ILeaveService leaves, IEmployeeService employees)
    {
        _leaves = leaves;
        _employees = employees;
    }

    private int CurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ELMS-13 — balance/used/remaining per DATABASE.md §6 + status breakdown.
    public async Task<IActionResult> Dashboard()
    {
        var userId = CurrentUserId();
        var user = await _employees.GetByIdAsync(userId);
        var (used, remaining) = await _employees.GetLeaveUsageAsync(userId);
        var history = await _leaves.GetHistoryAsync(userId);

        return View(new EmployeeDashboardViewModel
        {
            LeaveBalance = user?.LeaveBalance ?? 0,
            UsedDays = used,
            RemainingDays = remaining,
            PendingCount = history.Count(l => l.Status == LeaveStatus.Pending),
            ApprovedCount = history.Count(l => l.Status == LeaveStatus.Approved),
            RejectedCount = history.Count(l => l.Status == LeaveStatus.Rejected),
            RecentRequests = history.Take(5).ToList()
        });
    }

    // ELMS-09 — apply for leave.
    [HttpGet]
    public IActionResult Apply() => View(new ApplyLeaveViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(ApplyLeaveViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _leaves.SubmitRequestAsync(
            CurrentUserId(), model.FromDate, model.ToDate, model.Reason);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not submit the request.");
            return View(model);
        }

        TempData["Message"] = "Leave request submitted and is pending review.";
        return RedirectToAction(nameof(History));
    }

    // ELMS-10 — only the signed-in employee's own requests, never another's.
    [HttpGet]
    public async Task<IActionResult> History()
    {
        var history = await _leaves.GetHistoryAsync(CurrentUserId());
        return View(history);
    }

    // ELMS-20 — role-scoped help; view renders Employee-only FAQs + shared workflow strip.
    [HttpGet]
    public IActionResult Faq() => View();
}
