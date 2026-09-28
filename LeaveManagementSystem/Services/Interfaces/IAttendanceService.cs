using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Services.Interfaces;

public interface IAttendanceService
{
    Task<AttendanceRecord?> GetByIdAsync(int id);

    Task<AttendanceRecord?> GetTodayAsync(int userId);

    Task<ServiceResult<AttendanceRecord>> CheckInAsync(int userId);

    Task<ServiceResult<AttendanceRecord>> CheckOutAsync(int userId);

    Task<List<AttendanceRecord>> GetHistoryAsync(int userId, int take = 60);

    Task<List<AttendanceRecord>> GetByMonthAsync(int userId, int year, int month);

    Task<List<AttendanceRecord>> GetDailyAsync(DateTime date);

    Task<List<AttendanceRecord>> GetMonthlyAsync(int year, int month);

    Task<List<(User User, List<AttendanceRecord> Records)>> GetEmployeeWiseAsync(int year, int month, string? search);

    Task<ServiceResult<AttendanceRecord>> CorrectAsync(
        int recordId, AttendanceStatus status, string? remarks,
        decimal? workingHours = null, string? projectName = null,
        int actionByUserId = 0);

    Task<ServiceResult<AttendanceRecord>> UpsertAsync(
        int userId, DateTime date, AttendanceStatus status, string? remarks,
        decimal? workingHours = null, string? projectName = null,
        int actionByUserId = 0);

    // Phase 9 — employee self-submission for the CURRENT date only, via the
    // calendar popup (Date read-only, Working Hours + Project Name required).
    // Past/future dates, weekends, holidays and duplicates are rejected.
    Task<ServiceResult<AttendanceRecord>> SubmitDailyAttendanceAsync(
        int userId, DateTime date, decimal workingHours, string projectName, string? remarks);
}
