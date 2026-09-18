using LeaveManagementSystem.Models.Entities;

namespace LeaveManagementSystem.ViewModels;

public class EmployeeDashboardViewModel
{
    public int LeaveBalance { get; set; }

    public int UsedDays { get; set; }

    public int RemainingDays { get; set; }

    public int PendingCount { get; set; }

    public int ApprovedCount { get; set; }

    public int RejectedCount { get; set; }

    public List<LeaveRequest> RecentRequests { get; set; } = new();
}
