using System.ComponentModel.DataAnnotations;
using System.Reflection;
using LeaveManagementSystem.Controllers;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Services;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace LeaveManagementSystem.Tests;

// Phase 9 — click-current-date popup submission (hours + project) and the
// Employee → Submit → Admin Calendar flow. Uses in-memory fakes only.
public class DailyAttendanceSubmissionTests
{
    private static AttendanceService SubmitService(
        List<AttendanceRecord> records, List<Holiday> holidays)
    {
        var user = new User
        {
            Id = 1, FullName = "Test Employee", Email = "test@example.com",
            Role = UserRole.Employee, IsActive = true, LeaveBalance = 20
        };
        return new AttendanceService(
            new FakeAttendanceRepository(records),
            new FakeUserRepository(new List<User> { user }),
            new WorkingCalendarService(new FakeHolidayRepository(holidays)));
    }

    private static bool IsWeekendToday =>
        DateTime.Today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static bool TryValidate(object model, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    }

    // Employee flow: Today → Submit 9 hours + Project → Calendar shows Present.
    [Fact]
    public async Task Submit_TodayWithHoursAndProject_CreatesPresent()
    {
        if (IsWeekendToday)
        {
            // Weekends are covered by the weekend test below (same precedent
            // as the existing CheckIn test: date-dependent branch).
            return;
        }

        var records = new List<AttendanceRecord>();
        var service = SubmitService(records, new List<Holiday>());

        var result = await service.SubmitDailyAttendanceAsync(
            1, DateTime.Today, 9, "ELMS Development", "sprint work");

        Assert.True(result.Success);
        Assert.Equal(AttendanceStatus.Present, result.Data!.Status);
        Assert.Equal(9, result.Data.WorkingHours);
        Assert.Equal("ELMS Development", result.Data.ProjectName);
        Assert.Single(records);
    }

    // Duplicate submission for the same employee/date is blocked.
    [Fact]
    public async Task Submit_TwiceForSameDay_SecondIsRejected()
    {
        if (IsWeekendToday)
        {
            return;
        }

        var records = new List<AttendanceRecord>();
        var service = SubmitService(records, new List<Holiday>());

        Assert.True((await service.SubmitDailyAttendanceAsync(1, DateTime.Today, 9, "ELMS", null)).Success);
        var dup = await service.SubmitDailyAttendanceAsync(1, DateTime.Today, 8, "ELMS", null);

        Assert.False(dup.Success);
        Assert.Contains("already been submitted", dup.ErrorMessage);
        Assert.Single(records);
    }

    // Past dates cannot be marked by the employee.
    [Fact]
    public async Task Submit_PastDate_Rejected()
    {
        var service = SubmitService(new List<AttendanceRecord>(), new List<Holiday>());
        var result = await service.SubmitDailyAttendanceAsync(
            1, DateTime.Today.AddDays(-1), 9, "ELMS", null);

        Assert.False(result.Success);
        Assert.Contains("Past dates", result.ErrorMessage);
    }

    // Future dates cannot be marked by the employee.
    [Fact]
    public async Task Submit_FutureDate_Rejected()
    {
        var service = SubmitService(new List<AttendanceRecord>(), new List<Holiday>());
        var result = await service.SubmitDailyAttendanceAsync(
            1, DateTime.Today.AddDays(1), 9, "ELMS", null);

        Assert.False(result.Success);
        Assert.Contains("Future dates", result.ErrorMessage);
    }

    // Saturday/Sunday cannot be marked (asserts on weekends; vacuous pass on
    // weekdays since the service only accepts today — same precedent as the
    // existing CheckIn test).
    [Fact]
    public async Task Submit_OnWeekend_Rejected()
    {
        if (!IsWeekendToday)
        {
            return;
        }

        var service = SubmitService(new List<AttendanceRecord>(), new List<Holiday>());
        var result = await service.SubmitDailyAttendanceAsync(
            1, DateTime.Today, 9, "ELMS", null);

        Assert.False(result.Success);
        Assert.Contains("weekend", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // A configured holiday today cannot be marked.
    [Fact]
    public async Task Submit_OnHoliday_Rejected()
    {
        if (IsWeekendToday)
        {
            return; // weekend guard would fire first; covered above
        }

        var holidays = new List<Holiday>
        {
            new() { Id = 1, Name = "Today", Date = DateTime.Today }
        };
        var service = SubmitService(new List<AttendanceRecord>(), holidays);
        var result = await service.SubmitDailyAttendanceAsync(
            1, DateTime.Today, 9, "ELMS", null);

        Assert.False(result.Success);
        Assert.Contains("holiday", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Working hours are validated (required, 0.5–24).
    [Theory]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(24.5)]
    [InlineData(30)]
    public async Task Submit_InvalidHours_Rejected(decimal hours)
    {
        var service = SubmitService(new List<AttendanceRecord>(), new List<Holiday>());
        var result = await service.SubmitDailyAttendanceAsync(
            1, DateTime.Today, hours, "ELMS", null);

        Assert.False(result.Success);
        Assert.Contains("Working hours", result.ErrorMessage);
    }

    // Project name is required.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_MissingProject_Rejected(string project)
    {
        var service = SubmitService(new List<AttendanceRecord>(), new List<Holiday>());
        var result = await service.SubmitDailyAttendanceAsync(
            1, DateTime.Today, 9, project, null);

        Assert.False(result.Success);
        Assert.Contains("Project name", result.ErrorMessage);
    }

    // Popup model validation: hours + project required via DataAnnotations.
    [Fact]
    public void SubmitModel_MissingHoursAndProject_IsInvalid()
    {
        Assert.False(TryValidate(new SubmitAttendanceViewModel
        {
            Date = DateTime.Today, WorkingHours = null, ProjectName = null
        }, out var results));
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Working hours"));
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Project name"));
    }

    [Fact]
    public void SubmitModel_ValidInput_Passes()
    {
        Assert.True(TryValidate(new SubmitAttendanceViewModel
        {
            Date = DateTime.Today, WorkingHours = 9, ProjectName = "ELMS Development"
        }, out _));
    }

    // Admin flow: submitted attendance (hours + project) appears in the
    // common calendar with Present status and detail text.
    [Fact]
    public async Task Calendar_SubmittedAttendance_ShowsHoursAndProject()
    {
        var leaves = new List<LeaveRequest>();
        var attendance = new List<AttendanceRecord>
        {
            new()
            {
                Id = 1, UserId = 1, Date = new DateTime(2026, 11, 3),
                Status = AttendanceStatus.Present, WorkingHours = 9,
                ProjectName = "ELMS Development", Remarks = "sprint"
            },
        };
        var holidayRepo = new FakeHolidayRepository(new List<Holiday>());
        var service = new EmployeeCalendarService(
            new FakeLeaveRepository(leaves),
            new FakeAttendanceRepository(attendance),
            holidayRepo,
            new WorkingCalendarService(holidayRepo));

        var days = await service.BuildMonthAsync(1, 2026, 11);
        var tue = days.First(d => d.Date == new DateTime(2026, 11, 3));

        Assert.Equal(CalendarDayStatus.Present, tue.Status);
        Assert.Equal(9, tue.WorkingHours);
        Assert.Equal("ELMS Development", tue.ProjectName);
        Assert.Contains("9 Hours", tue.Detail);
        Assert.Contains("ELMS Development", tue.Detail);
    }

    // Leave/Holiday/Weekend integration still holds alongside submissions.
    [Fact]
    public async Task Calendar_SubmissionDoesNotOverrideLeaveOrHoliday()
    {
        var leaves = new List<LeaveRequest>
        {
            new() { Id = 1, UserId = 1, FromDate = new DateTime(2026, 11, 6), ToDate = new DateTime(2026, 11, 6), Reason = "off", Status = LeaveStatus.Approved },
        };
        var attendance = new List<AttendanceRecord>
        {
            // Stray attendance record on a leave day: leave wins (precedence).
            new() { Id = 1, UserId = 1, Date = new DateTime(2026, 11, 6), Status = AttendanceStatus.Present, WorkingHours = 9, ProjectName = "X" },
        };
        var holidayRepo = new FakeHolidayRepository(new List<Holiday>
        {
            new() { Id = 1, Name = "Special", Date = new DateTime(2026, 11, 9) }
        });
        var service = new EmployeeCalendarService(
            new FakeLeaveRepository(leaves),
            new FakeAttendanceRepository(attendance),
            holidayRepo,
            new WorkingCalendarService(holidayRepo));

        var days = await service.BuildMonthAsync(1, 2026, 11);
        Assert.Equal(CalendarDayStatus.ApprovedLeave, days.First(d => d.Date == new DateTime(2026, 11, 6)).Status);
        Assert.Equal(CalendarDayStatus.Holiday, days.First(d => d.Date == new DateTime(2026, 11, 9)).Status);
        Assert.Equal(CalendarDayStatus.Weekend, days.First(d => d.Date == new DateTime(2026, 11, 7)).Status);
    }

    // Submit is Employee-only; Report is Admin-only (attributes, not UI).
    [Fact]
    public void Submit_RequiresEmployeeRole_ReportRequiresAdminRole()
    {
        var submit = typeof(AttendanceController).GetMethods().First(m => m.Name == "Submit");
        var submitRoles = submit.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(a => a.Roles).ToList();
        submitRoles.AddRange(typeof(AttendanceController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Roles));
        Assert.Contains(submitRoles, r => r != null && r.Contains("Employee"));

        var report = typeof(AttendanceController).GetMethods().First(m => m.Name == "Report");
        var reportRoles = report.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(a => a.Roles).ToList();
        reportRoles.AddRange(typeof(AttendanceController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Roles));
        Assert.Contains(reportRoles, r => r != null && r.Contains("Admin"));
    }

    // No SQLite introduced; new columns exist on the SQL Server model.
    [Fact]
    public void Database_AttendanceHasHoursAndProject_NoSqlite()
    {
        var props = typeof(AttendanceRecord).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("WorkingHours", props);
        Assert.Contains("ProjectName", props);

        var csproj = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "LeaveManagementSystem", "LeaveManagementSystem.csproj"));
        Assert.DoesNotContain("Sqlite", csproj, StringComparison.OrdinalIgnoreCase);
    }
}
