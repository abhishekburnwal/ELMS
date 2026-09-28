using System.Reflection;
using LeaveManagementSystem.Controllers;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;

namespace LeaveManagementSystem.Tests;

// Phase 11 — Admin backdate fix: the employee date restriction must NOT apply
// to Admins, enforced by role-separated endpoints (not by trusting callers).
// In-memory fakes only; SQL Server Express untouched.
public class AdminBackdateTests
{
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

    private static DateTime RecentPast(DayOfWeek dow)
    {
        var d = DateTime.Today.AddDays(-1);
        while (d.DayOfWeek != dow)
        {
            d = d.AddDays(-1);
        }

        return d;
    }

    private static List<string> RolesOf(string action)
    {
        var methods = typeof(AttendanceController).GetMethods()
            .Where(m => m.Name == action).ToArray();
        Assert.NotEmpty(methods);
        var roles = new List<string>();
        foreach (var m in methods)
        {
            roles.AddRange(m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Select(a => a.Roles ?? string.Empty));
        }

        roles.AddRange(typeof(AttendanceController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(a => a.Roles ?? string.Empty));
        return roles.SelectMany(r => r.Split(',').Select(s => s.Trim())).ToList();
    }

    // The secure bypass: employee Submit is Employee-only AND today-only,
    // while admin Mark/Correct/Upsert are Admin-only with NO today-equality
    // rule — the role attribute, not caller honesty, grants the bypass.
    [Fact]
    public void Endpoints_AreRoleSeparated()
    {
        Assert.Contains("Employee", RolesOf("Submit"));
        Assert.DoesNotContain("Admin", RolesOf("Submit"));

        foreach (var action in new[] { "Mark", "Correct", "Report", "Daily", "Monthly", "EmployeeWise" })
        {
            Assert.Contains("Admin", RolesOf(action));
            Assert.DoesNotContain("Employee", RolesOf(action));
        }
    }

    // Same back date, two actors: employee rejected, admin succeeds.
    [Fact]
    public async Task SameBackDate_EmployeeRejected_AdminSucceeds()
    {
        var env = new Env();
        var monday = RecentPast(DayOfWeek.Monday);

        var employee = await env.Attendance().SubmitDailyAttendanceAsync(
            2, monday, 9, "ELMS", null);
        Assert.False(employee.Success);
        Assert.Contains("Past dates", employee.ErrorMessage);

        var admin = await env.Attendance().UpsertAsync(
            2, monday, AttendanceStatus.Present, "backdated by admin",
            9, "ELMS", actionByUserId: 1);
        Assert.True(admin.Success);
        Assert.Equal(9, admin.Data!.WorkingHours);
        Assert.Equal("ELMS", admin.Data.ProjectName);
    }

    // Admin backdate flips the calendar from Absent to Present.
    [Fact]
    public async Task AdminBackdate_FlipsAbsentToPresent()
    {
        var env = new Env();
        var monday = RecentPast(DayOfWeek.Monday);

        var before = await env.Calendar().BuildMonthAsync(2, monday.Year, monday.Month);
        Assert.Equal(CalendarDayStatus.Absent, before.First(d => d.Date == monday).Status);

        var admin = await env.Attendance().UpsertAsync(
            2, monday, AttendanceStatus.Present, null, 9, "ELMS", actionByUserId: 1);
        Assert.True(admin.Success);

        var after = await env.Calendar().BuildMonthAsync(2, monday.Year, monday.Month);
        var day = after.First(d => d.Date == monday);
        Assert.Equal(CalendarDayStatus.Present, day.Status);
        Assert.Contains("9 Hours", day.Detail);
        Assert.Contains("ELMS", day.Detail);
    }

    // Admin can also edit/correct an existing backdated timesheet.
    [Fact]
    public async Task AdminEdit_BackdatedRecord_UpdatesAndAudits()
    {
        var env = new Env();
        var monday = RecentPast(DayOfWeek.Monday);
        env.Records.Add(new AttendanceRecord
        {
            Id = 1, UserId = 2, Date = monday,
            Status = AttendanceStatus.Present, WorkingHours = 8, ProjectName = "Old"
        });

        var result = await env.Attendance().CorrectAsync(
            1, AttendanceStatus.Present, "corrected", 9, "ELMS", actionByUserId: 1);

        Assert.True(result.Success);
        Assert.Equal(9, result.Data!.WorkingHours);
        Assert.Contains(env.LeaveRepo.Audits, a =>
            a.AttendanceRecordId == 1 && a.Action == "Corrected" && a.ActionByUserId == 1);
    }

    // Backdate still respects Weekend/Holiday/Leave rendering.
    [Fact]
    public async Task Backdate_WeekendHolidayLeave_RulesHold()
    {
        var env = new Env();
        var saturday = RecentPast(DayOfWeek.Saturday);
        var wednesday = RecentPast(DayOfWeek.Wednesday);
        env.Holidays.Add(new Holiday { Id = 1, Name = "H", Date = wednesday });
        env.Leaves.Add(new LeaveRequest
        {
            Id = 1, UserId = 2, FromDate = RecentPast(DayOfWeek.Thursday),
            ToDate = RecentPast(DayOfWeek.Thursday), Reason = "off", Status = LeaveStatus.Approved
        });

        // Admin cannot overwrite a weekend or holiday with attendance…
        Assert.False((await env.Attendance().UpsertAsync(
            2, saturday, AttendanceStatus.Present, null, 8, "X", 1)).Success);
        Assert.False((await env.Attendance().UpsertAsync(
            2, wednesday, AttendanceStatus.Present, null, 8, "X", 1)).Success);

        // …and the calendar keeps rendering them (plus leave) correctly.
        var satDays = await env.Calendar().BuildMonthAsync(2, saturday.Year, saturday.Month);
        Assert.Equal(CalendarDayStatus.Weekend, satDays.First(d => d.Date == saturday).Status);
        var wedDays = await env.Calendar().BuildMonthAsync(2, wednesday.Year, wednesday.Month);
        Assert.Equal(CalendarDayStatus.Holiday, wedDays.First(d => d.Date == wednesday).Status);
    }

    // Admin cannot mark the future either — backdate only goes backwards.
    [Fact]
    public async Task Admin_FutureDate_Rejected()
    {
        var env = new Env();
        var result = await env.Attendance().UpsertAsync(
            2, DateTime.Today.AddDays(1), AttendanceStatus.Present, null, 8, "X", 1);

        Assert.False(result.Success);
        Assert.Contains("future", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
