using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Models.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public DateTime Date { get; set; }

    public DateTime? CheckIn { get; set; }

    public DateTime? CheckOut { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    // Phase 9 — daily attendance submission details: hours worked + project.
    public decimal? WorkingHours { get; set; }

    public string? ProjectName { get; set; }

    public string? Remarks { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedDate { get; set; }
}
