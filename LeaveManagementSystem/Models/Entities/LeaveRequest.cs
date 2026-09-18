using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Models.Entities;

public class LeaveRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public DateTime AppliedDate { get; set; } = DateTime.UtcNow;

    public int? ReviewedByUserId { get; set; }

    public User? ReviewedBy { get; set; }

    public DateTime? ReviewedDate { get; set; }

    public string? Remarks { get; set; }
}
