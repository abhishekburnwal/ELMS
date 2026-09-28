using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.Services;

// Centralized implementation — the ONLY place that decides
// working-day vs weekend vs holiday. Leave and Attendance both call here.
public class WorkingCalendarService : IWorkingCalendarService
{
    private readonly IHolidayRepository _holidays;

    public WorkingCalendarService(IHolidayRepository holidays)
    {
        _holidays = holidays;
    }

    public bool IsWeekend(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public async Task<bool> IsHolidayAsync(DateTime date)
    {
        if (IsWeekend(date))
        {
            return false; // weekends are Weekend, not Holiday
        }

        var set = await _holidays.GetDateSetInRangeAsync(date.Date, date.Date);
        return set.Contains(date.Date);
    }

    public async Task<bool> IsWorkingDayAsync(DateTime date)
    {
        if (IsWeekend(date))
        {
            return false;
        }

        return !await IsHolidayAsync(date);
    }

    public async Task<int> CountWorkingDaysAsync(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date;
        if (to < from)
        {
            throw new ArgumentException("To date cannot be earlier than from date.", nameof(toDate));
        }

        var holidaySet = await _holidays.GetDateSetInRangeAsync(from, to);
        return LeaveDaysCalculator.CountWorkingDays(from, to, holidaySet);
    }

    public Task<HashSet<DateTime>> GetHolidaysInRangeAsync(DateTime from, DateTime to) =>
        _holidays.GetDateSetInRangeAsync(from, to);
}
