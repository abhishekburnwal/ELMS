using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.Services;

public class EmployeeCalendarService : IEmployeeCalendarService
{
    private readonly ILeaveRepository _leaves;
    private readonly IAttendanceRepository _attendance;
    private readonly IHolidayRepository _holidays;
    private readonly IWorkingCalendarService _calendar;

    public EmployeeCalendarService(
        ILeaveRepository leaves,
        IAttendanceRepository attendance,
        IHolidayRepository holidays,
        IWorkingCalendarService calendar)
    {
        _leaves = leaves;
        _attendance = attendance;
        _holidays = holidays;
        _calendar = calendar;
    }

    public async Task<List<CalendarDay>> BuildMonthAsync(int userId, int year, int month)
    {
        year = Math.Clamp(year, 2000, 2100);
        month = Math.Clamp(month, 1, 12);
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var today = DateTime.Today;

        var leaves = await _leaves.GetByUserAsync(userId);
        var attendance = await _attendance.GetByUserInRangeAsync(userId, from, to);
        var holidaySet = await _holidays.GetDateSetInRangeAsync(from, to);
        var attendanceByDate = attendance.ToDictionary(a => a.Date.Date, a => a);

        var days = new List<CalendarDay>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var date = day.Date;
            CalendarDay entry;

            if (_calendar.IsWeekend(date))
            {
                entry = new CalendarDay { Date = date, Status = CalendarDayStatus.Weekend, Label = "Weekend" };
            }
            else if (holidaySet.Contains(date))
            {
                entry = new CalendarDay { Date = date, Status = CalendarDayStatus.Holiday, Label = "Holiday" };
            }
            else if (FindLeave(leaves, date, LeaveStatus.Approved) is { } approved)
            {
                entry = new CalendarDay
                {
                    Date = date,
                    Status = CalendarDayStatus.ApprovedLeave,
                    Label = "Leave",
                    Detail = approved.Reason
                };
            }
            else if (FindLeave(leaves, date, LeaveStatus.Pending) is { } pending)
            {
                entry = new CalendarDay
                {
                    Date = date,
                    Status = CalendarDayStatus.PendingLeave,
                    Label = "Pending Leave",
                    Detail = pending.Reason
                };
            }
            else if (attendanceByDate.TryGetValue(date, out var record))
            {
                entry = MapAttendance(record, date);
            }
            else if (FindLeave(leaves, date, LeaveStatus.Rejected) is not null)
            {
                // Rejected leave is NOT leave — fall through to attendance logic.
                entry = date < today
                    ? new CalendarDay { Date = date, Status = CalendarDayStatus.Absent, Label = "Absent", Detail = "Leave rejected" }
                    : new CalendarDay { Date = date, Status = CalendarDayStatus.NoRecord, Label = "—", Detail = "Leave rejected" };
            }
            else if (date < today)
            {
                entry = new CalendarDay { Date = date, Status = CalendarDayStatus.Absent, Label = "Absent" };
            }
            else
            {
                entry = new CalendarDay { Date = date, Status = CalendarDayStatus.NoRecord, Label = "—" };
            }

            days.Add(entry);
        }

        return days;
    }

    private static Models.Entities.LeaveRequest? FindLeave(
        List<Models.Entities.LeaveRequest> leaves, DateTime date, LeaveStatus status)
    {
        return leaves.FirstOrDefault(l =>
            l.Status == status && l.FromDate.Date <= date && date <= l.ToDate.Date);
    }

    private static CalendarDay MapAttendance(Models.Entities.AttendanceRecord record, DateTime date)
    {
        var day = record.Status switch
        {
            AttendanceStatus.Present => new CalendarDay { Date = date, Status = CalendarDayStatus.Present, Label = "Present" },
            AttendanceStatus.HalfDay => new CalendarDay { Date = date, Status = CalendarDayStatus.HalfDay, Label = "Half Day" },
            AttendanceStatus.WorkFromHome => new CalendarDay { Date = date, Status = CalendarDayStatus.WorkFromHome, Label = "WFH" },
            AttendanceStatus.Leave => new CalendarDay { Date = date, Status = CalendarDayStatus.ApprovedLeave, Label = "Leave" },
            AttendanceStatus.Holiday => new CalendarDay { Date = date, Status = CalendarDayStatus.Holiday, Label = "Holiday" },
            AttendanceStatus.Weekend => new CalendarDay { Date = date, Status = CalendarDayStatus.Weekend, Label = "Weekend" },
            _ => new CalendarDay { Date = date, Status = CalendarDayStatus.Absent, Label = "Absent" },
        };

        // Phase 9 — carry the submission details so both the employee and the
        // admin calendar can show hours + project without extra queries.
        day.AttendanceId = record.Id;
        day.WorkingHours = record.WorkingHours;
        day.ProjectName = record.ProjectName;
        day.Remarks = record.Remarks;
        if (record.WorkingHours.HasValue || !string.IsNullOrWhiteSpace(record.ProjectName))
        {
            var parts = new List<string>();
            if (record.WorkingHours.HasValue)
            {
                parts.Add($"{record.WorkingHours.Value:0.##} Hours");
            }

            if (!string.IsNullOrWhiteSpace(record.ProjectName))
            {
                parts.Add($"Project: {record.ProjectName}");
            }

            day.Detail = string.Join(" · ", parts);
        }

        return day;
    }
}
