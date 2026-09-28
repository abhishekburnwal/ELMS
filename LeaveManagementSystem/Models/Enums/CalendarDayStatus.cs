namespace LeaveManagementSystem.Models.Enums;

// Display status for the common Employee Calendar (Leave + Attendance + Holidays).
// Precedence when resolving a day: Weekend > Holiday > ApprovedLeave >
// PendingLeave > Attendance record > Absent (past working day) / empty (future).
public enum CalendarDayStatus
{
    Present = 0,
    Absent = 1,
    ApprovedLeave = 2,
    PendingLeave = 3,
    RejectedLeave = 4,
    Holiday = 5,
    Weekend = 6,
    HalfDay = 7,
    WorkFromHome = 8,
    NoRecord = 9
}
