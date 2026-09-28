using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Services.Interfaces;

// Single centralized Working Day / Calendar service.
// Monday–Friday = Working Day, Saturday/Sunday = Weekend,
// admin-configured Holiday = Holiday.
// BOTH Leave Management and Attendance MUST use this service —
// no separate working-day calculations elsewhere.
public interface IWorkingCalendarService
{
    bool IsWeekend(DateTime date);

    Task<bool> IsHolidayAsync(DateTime date);

    Task<bool> IsWorkingDayAsync(DateTime date);

    Task<int> CountWorkingDaysAsync(DateTime fromDate, DateTime toDate);

    Task<HashSet<DateTime>> GetHolidaysInRangeAsync(DateTime from, DateTime to);
}
