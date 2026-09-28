namespace LeaveManagementSystem.Models.Entities;

// Who approved/rejected a request and when (DATABASE.md §3, ELMS-19),
// plus who changed an attendance/timesheet record and what changed
// (Phase 10). Exactly one of LeaveRequestId / AttendanceRecordId is set.
public class AuditLog
{
    public int Id { get; set; }

    public int? LeaveRequestId { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }

    public int? AttendanceRecordId { get; set; }

    public AttendanceRecord? AttendanceRecord { get; set; }

    public int ActionByUserId { get; set; }

    public User? ActionBy { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Details { get; set; }

    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
}
