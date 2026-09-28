namespace LeaveManagementSystem.Services;

// Single source of truth for leave duration (working-days rule):
// the inclusive date range counts Monday–Friday only; Saturday and
// Sunday are office-closed and never contribute. Configured holidays
// are excluded via the overload below (used by WorkingCalendarService).
// All balance, summary, export, and view calculations must go through
// here or WorkingCalendarService — never inline
// `(ToDate - FromDate).Days + 1` calendar-day math.
public static class LeaveDaysCalculator
{
    public static int CountWorkingDays(DateTime fromDate, DateTime toDate) =>
        CountWorkingDays(fromDate, toDate, holidays: null);

    public static int CountWorkingDays(
        DateTime fromDate, DateTime toDate, ICollection<DateTime>? holidays)
    {
        var from = fromDate.Date;
        var to = toDate.Date;

        if (to < from)
        {
            throw new ArgumentException("To date cannot be earlier than from date.", nameof(toDate));
        }

        HashSet<DateTime>? holidaySet = null;
        if (holidays is { Count: > 0 })
        {
            holidaySet = holidays.Select(h => h.Date).ToHashSet();
        }

        var count = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            if (holidaySet is not null && holidaySet.Contains(day))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}
