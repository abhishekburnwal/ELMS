namespace LeaveManagementSystem.Models.Entities;

// Who approved/rejected a request and when (DATABASE.md §3, ELMS-19).
public class AuditLog
{
    public int Id { get; set; }

    public int LeaveRequestId { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }

    public int ActionByUserId { get; set; }

    public User? ActionBy { get; set; }

    public string Action { get; set; } = string.Empty;

    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
}
