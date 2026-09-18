namespace LeaveManagementSystem.Services;

// Single source of truth for leave duration (working-days rule):
// the inclusive date range counts Monday–Friday only; Saturday and
// Sunday are office-closed and never contribute. All balance, summary,
// export, and view calculations must go through here — never inline
// `(ToDate - FromDate).Days + 1` calendar-day math.
public static class LeaveDaysCalculator
{
    public static int CountWorkingDays(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date;

        if (to < from)
        {
            throw new ArgumentException("To date cannot be earlier than from date.", nameof(toDate));
        }

        var count = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                count++;
            }
        }

        return count;
    }
}
