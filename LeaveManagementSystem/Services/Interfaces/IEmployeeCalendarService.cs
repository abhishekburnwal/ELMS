using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Services.Interfaces;

public sealed class CalendarDay
{
    public DateTime Date { get; set; }

    public CalendarDayStatus Status { get; set; }

    public string Label { get; set; } = string.Empty;

    public string? Detail { get; set; }

    // Phase 9 — attendance submission details shown on the calendar
    // (hours + project), e.g. "Present, 9 Hours, Project: ELMS Development".
    public decimal? WorkingHours { get; set; }

    public string? ProjectName { get; set; }

    public string? Remarks { get; set; }

    public int? AttendanceId { get; set; }
}

public interface IEmployeeCalendarService
{
    // One common calendar for a user + month, merging (in precedence order):
    // Weekend > Holiday > ApprovedLeave > PendingLeave > Attendance >
    // Absent (past working day) / NoRecord (future working day).
    // Rejected leaves are never shown as leave — the day falls through to
    // attendance/ absent logic. Reads existing records only, never duplicates.
    Task<List<CalendarDay>> BuildMonthAsync(int userId, int year, int month);
}
