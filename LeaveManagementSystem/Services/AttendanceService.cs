using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendance;
    private readonly IUserRepository _users;
    private readonly IWorkingCalendarService _calendar;
    private readonly IAuditService _audit;

    public AttendanceService(
        IAttendanceRepository attendance,
        IUserRepository users,
        IWorkingCalendarService calendar,
        IAuditService? audit = null)
    {
        _attendance = attendance;
        _users = users;
        _calendar = calendar;
        _audit = audit ?? NullAuditService.Instance;
    }

    // Fallback when constructed without an audit store (older unit tests):
    // keeps the service usable while production DI always supplies one.
    private sealed class NullAuditService : IAuditService
    {
        public static readonly NullAuditService Instance = new();

        public Task RecordDecisionAsync(int leaveRequestId, int actionByUserId, string action) =>
            Task.CompletedTask;

        public Task RecordAttendanceChangeAsync(
            int? attendanceRecordId, int actionByUserId, string action, string? details) =>
            Task.CompletedTask;
    }

    private static string Describe(AttendanceRecord record)
    {
        var parts = new List<string> { record.Date.ToString("yyyy-MM-dd"), record.Status.ToString() };
        if (record.WorkingHours.HasValue)
        {
            parts.Add($"{record.WorkingHours.Value:0.##}h");
        }

        if (!string.IsNullOrWhiteSpace(record.ProjectName))
        {
            parts.Add(record.ProjectName.Trim());
        }

        return string.Join(" · ", parts);
    }

    public Task<AttendanceRecord?> GetByIdAsync(int id) =>
        _attendance.GetByIdAsync(id);

    public Task<AttendanceRecord?> GetTodayAsync(int userId) =>
        _attendance.GetByUserAndDateAsync(userId, DateTime.Today);

    public async Task<ServiceResult<AttendanceRecord>> CheckInAsync(int userId)
    {
        var today = DateTime.Today;
        if (!_calendar.IsWeekend(today) && await _calendar.IsHolidayAsync(today))
        {
            return ServiceResult<AttendanceRecord>.Fail("Today is a configured holiday — check-in is disabled.");
        }

        if (_calendar.IsWeekend(today))
        {
            return ServiceResult<AttendanceRecord>.Fail("Today is a weekend — check-in is disabled.");
        }

        var existing = await _attendance.GetByUserAndDateAsync(userId, today);
        if (existing is not null)
        {
            if (existing.CheckIn.HasValue)
            {
                return ServiceResult<AttendanceRecord>.Fail("You have already checked in today.");
            }

            existing.CheckIn = DateTime.Now;
            existing.Status = AttendanceStatus.Present;
            existing.UpdatedDate = DateTime.UtcNow;
            await _attendance.UpdateAsync(existing);
            await _attendance.SaveChangesAsync();
            return ServiceResult<AttendanceRecord>.Ok(existing);
        }

        var record = new AttendanceRecord
        {
            UserId = userId,
            Date = today,
            CheckIn = DateTime.Now,
            Status = AttendanceStatus.Present,
            CreatedDate = DateTime.UtcNow
        };
        await _attendance.AddAsync(record);
        await _attendance.SaveChangesAsync();
        return ServiceResult<AttendanceRecord>.Ok(record);
    }

    public async Task<ServiceResult<AttendanceRecord>> CheckOutAsync(int userId)
    {
        var today = DateTime.Today;
        var existing = await _attendance.GetByUserAndDateAsync(userId, today);
        if (existing?.CheckIn is null)
        {
            return ServiceResult<AttendanceRecord>.Fail("Check in first before checking out.");
        }

        if (existing.CheckOut.HasValue)
        {
            return ServiceResult<AttendanceRecord>.Fail("You have already checked out today.");
        }

        existing.CheckOut = DateTime.Now;
        existing.UpdatedDate = DateTime.UtcNow;
        await _attendance.UpdateAsync(existing);
        await _attendance.SaveChangesAsync();
        return ServiceResult<AttendanceRecord>.Ok(existing);
    }

    public async Task<List<AttendanceRecord>> GetHistoryAsync(int userId, int take = 60)
    {
        var to = DateTime.Today;
        var from = to.AddDays(-take + 1);
        var records = await _attendance.GetByUserInRangeAsync(userId, from, to);
        return records.OrderByDescending(r => r.Date).ToList();
    }

    public async Task<List<AttendanceRecord>> GetByMonthAsync(int userId, int year, int month)
    {
        year = Math.Clamp(year, 2000, 2100);
        month = Math.Clamp(month, 1, 12);
        return await _attendance.GetByUserAndMonthAsync(userId, year, month);
    }

    public Task<List<AttendanceRecord>> GetDailyAsync(DateTime date) =>
        _attendance.GetByDateAsync(date.Date);

    public Task<List<AttendanceRecord>> GetMonthlyAsync(int year, int month)
    {
        year = Math.Clamp(year, 2000, 2100);
        month = Math.Clamp(month, 1, 12);
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return _attendance.GetInRangeAsync(from, to);
    }

    public async Task<List<(User User, List<AttendanceRecord> Records)>> GetEmployeeWiseAsync(
        int year, int month, string? search)
    {
        year = Math.Clamp(year, 2000, 2100);
        month = Math.Clamp(month, 1, 12);
        var users = await _users.GetEmployeesAsync(search);
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var all = await _attendance.GetInRangeAsync(from, to);
        return users.Select(u => (u, all.Where(a => a.UserId == u.Id).OrderBy(a => a.Date).ToList())).ToList();
    }

    public async Task<ServiceResult<AttendanceRecord>> CorrectAsync(
        int recordId, AttendanceStatus status, string? remarks,
        decimal? workingHours = null, string? projectName = null,
        int actionByUserId = 0)
    {
        var record = await _attendance.GetByIdAsync(recordId);
        if (record is null)
        {
            return ServiceResult<AttendanceRecord>.Fail("Attendance record not found.");
        }

        var hoursCheck = ValidateHours(workingHours);
        if (hoursCheck is not null)
        {
            return hoursCheck;
        }

        var projectCheck = ValidateProject(projectName, allowNull: true);
        if (projectCheck is not null)
        {
            return projectCheck;
        }

        record.Status = status;
        record.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        if (workingHours.HasValue)
        {
            record.WorkingHours = workingHours.Value;
        }

        if (projectName is not null)
        {
            record.ProjectName = string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
        }

        record.UpdatedDate = DateTime.UtcNow;
        await _attendance.UpdateAsync(record);

        // Phase 10 — every admin correction is audited in the same
        // transaction as the change itself.
        await _audit.RecordAttendanceChangeAsync(
            record.Id, actionByUserId, "Corrected", Describe(record));

        await _attendance.SaveChangesAsync();
        return ServiceResult<AttendanceRecord>.Ok(record);
    }

    public async Task<ServiceResult<AttendanceRecord>> UpsertAsync(
        int userId, DateTime date, AttendanceStatus status, string? remarks,
        decimal? workingHours = null, string? projectName = null,
        int actionByUserId = 0)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            return ServiceResult<AttendanceRecord>.Fail("Employee not found.");
        }

        var day = date.Date;
        if (day > DateTime.Today)
        {
            return ServiceResult<AttendanceRecord>.Fail("Attendance cannot be marked for a future date.");
        }

        // Admins work on applicable working dates only — weekends and
        // configured holidays stay Weekend/Holiday, never attendance.
        if (_calendar.IsWeekend(day))
        {
            return ServiceResult<AttendanceRecord>.Fail("Saturday and Sunday are weekends — attendance cannot be marked for this date.");
        }

        if (await _calendar.IsHolidayAsync(day))
        {
            return ServiceResult<AttendanceRecord>.Fail("This date is a configured holiday — attendance cannot be marked for it.");
        }

        var hoursCheck = ValidateHours(workingHours);
        if (hoursCheck is not null)
        {
            return hoursCheck;
        }

        var projectCheck = ValidateProject(projectName, allowNull: true);
        if (projectCheck is not null)
        {
            return projectCheck;
        }

        var existing = await _attendance.GetByUserAndDateAsync(userId, day);
        if (existing is null)
        {
            var record = new AttendanceRecord
            {
                UserId = userId,
                Date = day,
                Status = status,
                Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
                WorkingHours = workingHours,
                ProjectName = string.IsNullOrWhiteSpace(projectName) ? null : projectName!.Trim(),
                CreatedDate = DateTime.UtcNow
            };
            await _attendance.AddAsync(record);
            await _attendance.SaveChangesAsync();

            // Phase 10 — audit the backfill (record id is known after save).
            await _audit.RecordAttendanceChangeAsync(
                record.Id, actionByUserId, "Added", Describe(record));
            await _attendance.SaveChangesAsync();

            return ServiceResult<AttendanceRecord>.Ok(record);
        }

        existing.Status = status;
        existing.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        if (workingHours.HasValue)
        {
            existing.WorkingHours = workingHours.Value;
        }

        if (projectName is not null)
        {
            existing.ProjectName = string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
        }

        existing.UpdatedDate = DateTime.UtcNow;
        await _attendance.UpdateAsync(existing);

        await _audit.RecordAttendanceChangeAsync(
            existing.Id, actionByUserId, "Corrected", Describe(existing));

        await _attendance.SaveChangesAsync();
        return ServiceResult<AttendanceRecord>.Ok(existing);
    }

    // Phase 9 — employee self-submission: current date only, no duplicates,
    // no weekends/holidays, hours + project mandatory. Backend is the source
    // of truth; the popup's read-only date is never trusted on its own.
    public async Task<ServiceResult<AttendanceRecord>> SubmitDailyAttendanceAsync(
        int userId, DateTime date, decimal workingHours, string projectName, string? remarks)
    {
        var day = date.Date;
        var today = DateTime.Today;

        if (day != today)
        {
            return ServiceResult<AttendanceRecord>.Fail(day < today
                ? "Past dates cannot be marked by the employee."
                : "Future dates cannot be marked by the employee.");
        }

        // Input validation runs before weekend/holiday checks so invalid
        // hours or a missing project is reported deterministically.
        var hoursCheck = ValidateHours(workingHours, required: true);
        if (hoursCheck is not null)
        {
            return hoursCheck;
        }

        var projectCheck = ValidateProject(projectName, allowNull: false);
        if (projectCheck is not null)
        {
            return projectCheck;
        }

        if (!string.IsNullOrWhiteSpace(remarks) && remarks.Trim().Length > 500)
        {
            return ServiceResult<AttendanceRecord>.Fail("Remarks cannot exceed 500 characters.", nameof(remarks));
        }

        if (_calendar.IsWeekend(day))
        {
            return ServiceResult<AttendanceRecord>.Fail("Saturday and Sunday are weekends — attendance cannot be marked.");
        }

        if (await _calendar.IsHolidayAsync(day))
        {
            return ServiceResult<AttendanceRecord>.Fail("Today is a configured holiday — attendance cannot be marked.");
        }

        var existing = await _attendance.GetByUserAndDateAsync(userId, day);
        if (existing is not null)
        {
            return ServiceResult<AttendanceRecord>.Fail("Attendance for today has already been submitted.");
        }

        var record = new AttendanceRecord
        {
            UserId = userId,
            Date = day,
            CheckIn = DateTime.Now,
            Status = AttendanceStatus.Present,
            WorkingHours = workingHours,
            ProjectName = projectName.Trim(),
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
            CreatedDate = DateTime.UtcNow
        };
        await _attendance.AddAsync(record);
        await _attendance.SaveChangesAsync();
        return ServiceResult<AttendanceRecord>.Ok(record);
    }

    private static ServiceResult<AttendanceRecord>? ValidateHours(decimal? hours, bool required = false)
    {
        if (!hours.HasValue)
        {
            return required
                ? ServiceResult<AttendanceRecord>.Fail("Working hours are required.", nameof(hours))
                : null;
        }

        if (hours.Value < 0.5m || hours.Value > 24m)
        {
            return ServiceResult<AttendanceRecord>.Fail("Working hours must be between 0.5 and 24.", nameof(hours));
        }

        return null;
    }

    private static ServiceResult<AttendanceRecord>? ValidateProject(string? project, bool allowNull)
    {
        if (project is null)
        {
            return allowNull
                ? null
                : ServiceResult<AttendanceRecord>.Fail("Project name is required.", nameof(project));
        }

        if (!allowNull && string.IsNullOrWhiteSpace(project))
        {
            return ServiceResult<AttendanceRecord>.Fail("Project name is required.", nameof(project));
        }

        if (project.Trim().Length > 200)
        {
            return ServiceResult<AttendanceRecord>.Fail("Project name cannot exceed 200 characters.", nameof(project));
        }

        return null;
    }
}
