using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Services;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.Tests;

// Phase 10 — Timesheet rules: current-day only submission, missed day → Absent
// with no backfill, Weekend/Holiday/Leave priority, admin correction + audit.
// In-memory fakes only; SQL Server Express untouched.
public class TimesheetRulesTests
{
    // All calendar assertions use strictly-past dates derived from today, so
    // missed working days resolve to Absent (not NoRecord) on any run date.

    private static DateTime RecentPast(DayOfWeek dow)
    {
        var d = DateTime.Today.AddDays(-1);
        while (d.DayOfWeek != dow)
        {
            d = d.AddDays(-1);
        }

        return d;
    }

    private sealed class Env
    {
        public List<AttendanceRecord> Records { get; } = new();
        public List<Holiday> Holidays { get; } = new();
        public List<LeaveRequest> Leaves { get; } = new();
        public FakeLeaveRepository LeaveRepo { get; }
        public FakeAudit Audit { get; }
        public List<User> Users { get; } = new()
        {
            new() { Id = 1, FullName = "Admin", Email = "admin@example.com", Role = UserRole.Admin, IsActive = true },
            new() { Id = 2, FullName = "Emp", Email = "emp@example.com", Role = UserRole.Employee, IsActive = true, LeaveBalance = 20 },
        };

        public Env()
        {
            LeaveRepo = new FakeLeaveRepository(Leaves);
            Audit = new FakeAudit(LeaveRepo);
        }

        public AttendanceService Attendance() => new(
            new FakeAttendanceRepository(Records),
            new FakeUserRepository(Users),
            new WorkingCalendarService(new FakeHolidayRepository(Holidays)),
            Audit);

        public EmployeeCalendarService Calendar()
        {
            var holidayRepo = new FakeHolidayRepository(Holidays);
            return new EmployeeCalendarService(
                new FakeLeaveRepository(Leaves),
                new FakeAttendanceRepository(Records),
                holidayRepo,
                new WorkingCalendarService(holidayRepo));
        }
    }

    private static bool IsWeekendToday =>
        DateTime.Today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    // Rule: Monday submitted → PRESENT with hours + project.
    [Fact]
    public async Task Monday_Submitted_ShowsPresent()
    {
        var env = new Env();
        var monday = RecentPast(DayOfWeek.Monday);
        env.Records.Add(new AttendanceRecord
        {
            Id = 1, UserId = 2, Date = monday,
            Status = AttendanceStatus.Present, WorkingHours = 9, ProjectName = "ELMS"
        });

        var days = await env.Calendar().BuildMonthAsync(2, monday.Year, monday.Month);
        var day = days.First(d => d.Date == monday);

        Assert.Equal(CalendarDayStatus.Present, day.Status);
        Assert.Contains("9 Hours", day.Detail);
        Assert.Contains("ELMS", day.Detail);
    }

    // Rule: Monday missed → ABSENT on Tuesday, and the employee cannot
    // backfill it (API bypass attempt with yesterday's date is rejected).
    [Fact]
    public async Task Monday_Missed_ShowsAbsent_AndCannotBeBackfilled()
    {
        var env = new Env();
        var monday = RecentPast(DayOfWeek.Monday);

        var days = await env.Calendar().BuildMonthAsync(2, monday.Year, monday.Month);
        Assert.Equal(CalendarDayStatus.Absent, days.First(d => d.Date == monday).Status);

        var bypass = await env.Attendance().SubmitDailyAttendanceAsync(
            2, DateTime.Today.AddDays(-1), 9, "ELMS", null);
        Assert.False(bypass.Success);
        Assert.Contains("Past dates", bypass.ErrorMessage);
        Assert.Empty(env.Records);
    }

    // Rule: Sat/Sun show Weekend, never Absent — even with no timesheet.
    [Fact]
    public async Task Weekend_WithNoTimesheet_ShowsWeekend_NotAbsent()
    {
        var env = new Env();
        var saturday = RecentPast(DayOfWeek.Saturday);
        var sunday = RecentPast(DayOfWeek.Sunday);

        var satDays = await env.Calendar().BuildMonthAsync(2, saturday.Year, saturday.Month);
        Assert.Equal(CalendarDayStatus.Weekend, satDays.First(d => d.Date == saturday).Status);

        var sunDays = await env.Calendar().BuildMonthAsync(2, sunday.Year, sunday.Month);
        Assert.Equal(CalendarDayStatus.Weekend, sunDays.First(d => d.Date == sunday).Status);
    }

    // Rule: configured holiday shows Holiday, never Absent.
    [Fact]
    public async Task Holiday_WithNoTimesheet_ShowsHoliday_NotAbsent()
    {
        var env = new Env();
        var wednesday = RecentPast(DayOfWeek.Wednesday);
        env.Holidays.Add(new Holiday { Id = 1, Name = "Special", Date = wednesday });

        var days = await env.Calendar().BuildMonthAsync(2, wednesday.Year, wednesday.Month);
        Assert.Equal(CalendarDayStatus.Holiday, days.First(d => d.Date == wednesday).Status);
    }

    // Rule: approved leave shows Leave, never Absent.
    [Fact]
    public async Task ApprovedLeave_WithNoTimesheet_ShowsLeave_NotAbsent()
    {
        var env = new Env();
        var thursday = RecentPast(DayOfWeek.Thursday);
        env.Leaves.Add(new LeaveRequest
        {
            Id = 1, UserId = 2, FromDate = thursday,
            ToDate = thursday, Reason = "off", Status = LeaveStatus.Approved
        });

        var days = await env.Calendar().BuildMonthAsync(2, thursday.Year, thursday.Month);
        Assert.Equal(CalendarDayStatus.ApprovedLeave, days.First(d => d.Date == thursday).Status);
    }

    // Rule: admin CAN correct a missed past working day (backfill allowed
    // for admins only), with hours + project + remarks.
    [Fact]
    public async Task Admin_CorrectsMissedMonday_Succeeds()
    {
        var env = new Env();
        var monday = RecentPast(DayOfWeek.Monday);

        var result = await env.Attendance().UpsertAsync(
            2, monday, AttendanceStatus.Present, "admin backfill",
            9, "ELMS", actionByUserId: 1);

        Assert.True(result.Success);
        Assert.Equal(9, result.Data!.WorkingHours);
        Assert.Equal("ELMS", result.Data.ProjectName);
        Assert.Contains(env.LeaveRepo.Audits, a =>
            a.AttendanceRecordId == result.Data.Id && a.ActionByUserId == 1 && a.Action == "Added");
    }

    // Rule: every admin correction is recorded in the audit log.
    [Fact]
    public async Task Admin_CorrectExistingRecord_WritesAudit()
    {
        var env = new Env();
        var tuesday = RecentPast(DayOfWeek.Tuesday);
        env.Records.Add(new AttendanceRecord
        {
            Id = 1, UserId = 2, Date = tuesday,
            Status = AttendanceStatus.Present, WorkingHours = 8, ProjectName = "Old"
        });

        var result = await env.Attendance().CorrectAsync(
            1, AttendanceStatus.Present, "fixed", 9, "ELMS", actionByUserId: 1);

        Assert.True(result.Success);
        var audit = Assert.Single(env.LeaveRepo.Audits, a =>
            a.AttendanceRecordId == 1 && a.Action == "Corrected");
        Assert.Equal(1, audit.ActionByUserId);
        Assert.Contains("ELMS", audit.Details);
    }

    // Rule: admin cannot mark weekends or holidays ("applicable working date").
    [Fact]
    public async Task Admin_UpsertWeekendOrHoliday_Rejected()
    {
        var env = new Env();
        var saturday = RecentPast(DayOfWeek.Saturday);
        var wednesday = RecentPast(DayOfWeek.Wednesday);
        env.Holidays.Add(new Holiday { Id = 1, Name = "H", Date = wednesday });

        var sat = await env.Attendance().UpsertAsync(
            2, saturday, AttendanceStatus.Present, null, 8, "X", 1);
        Assert.False(sat.Success);
        Assert.Contains("weekend", sat.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var hol = await env.Attendance().UpsertAsync(
            2, wednesday, AttendanceStatus.Present, null, 8, "X", 1);
        Assert.False(hol.Success);
        Assert.Contains("holiday", hol.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(env.LeaveRepo.Audits); // rejected writes leave no audit trail
    }

    // Rule: current-day employee submission still works end to end.
    [Fact]
    public async Task CurrentDay_Submission_CreatesPresent()
    {
        if (IsWeekendToday)
        {
            return; // weekend path covered by weekend rule tests
        }

        var env = new Env();
        var result = await env.Attendance().SubmitDailyAttendanceAsync(
            2, DateTime.Today, 9, "ELMS", "sprint");

        Assert.True(result.Success);
        Assert.Equal(CalendarDayStatus.Present,
            (await env.Calendar().BuildMonthAsync(2, DateTime.Today.Year, DateTime.Today.Month))
            .First(d => d.Date == DateTime.Today).Status);
    }

    // Audit schema supports attendance entries independently of leave.
    [Fact]
    public void AuditLog_SupportsAttendanceEntries()
    {
        var props = typeof(AttendanceRecord).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("WorkingHours", props);

        var auditProps = typeof(AuditLog).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("AttendanceRecordId", auditProps);
        Assert.Contains("Details", auditProps);
        Assert.True(typeof(AuditLog).GetProperty("LeaveRequestId")!.PropertyType == typeof(int?));
    }
}
