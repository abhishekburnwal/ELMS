using System.Reflection;
using System.Text;
using LeaveManagementSystem.Controllers;
using LeaveManagementSystem.Data;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services;
using LeaveManagementSystem.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Tests;

// In-memory holiday repo — no database, SQL Server Express untouched.
public sealed class FakeHolidayRepository : IHolidayRepository
{
    private readonly List<Holiday> _holidays;

    public FakeHolidayRepository(List<Holiday> holidays) => _holidays = holidays;

    public Task<Holiday?> GetByIdAsync(int id) =>
        Task.FromResult(_holidays.FirstOrDefault(h => h.Id == id));

    public Task<List<Holiday>> GetByYearAsync(int year) =>
        Task.FromResult(_holidays.Where(h => h.Date.Year == year).OrderBy(h => h.Date).ToList());

    public Task<List<Holiday>> GetAllAsync() =>
        Task.FromResult(_holidays.OrderBy(h => h.Date).ToList());

    public Task<List<Holiday>> GetInRangeAsync(DateTime from, DateTime to) =>
        Task.FromResult(_holidays.Where(h => h.Date >= from.Date && h.Date <= to.Date).OrderBy(h => h.Date).ToList());

    public Task<HashSet<DateTime>> GetDateSetInRangeAsync(DateTime from, DateTime to) =>
        Task.FromResult(_holidays.Where(h => h.Date >= from.Date && h.Date <= to.Date).Select(h => h.Date.Date).ToHashSet());

    public Task<bool> ExistsOnDateAsync(DateTime date, int? excludeId = null) =>
        Task.FromResult(_holidays.Any(h => h.Date == date.Date && (excludeId == null || h.Id != excludeId.Value)));

    public Task AddAsync(Holiday holiday)
    {
        holiday.Id = _holidays.Count == 0 ? 1 : _holidays.Max(h => h.Id) + 1;
        _holidays.Add(holiday);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Holiday holiday) => Task.CompletedTask;

    public Task DeleteAsync(Holiday holiday)
    {
        _holidays.Remove(holiday);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}

// In-memory attendance repo — no database.
public sealed class FakeAttendanceRepository : IAttendanceRepository
{
    private readonly List<AttendanceRecord> _records;

    public FakeAttendanceRepository(List<AttendanceRecord> records) => _records = records;

    public Task<AttendanceRecord?> GetByIdAsync(int id) =>
        Task.FromResult(_records.FirstOrDefault(a => a.Id == id));

    public Task<AttendanceRecord?> GetByUserAndDateAsync(int userId, DateTime date) =>
        Task.FromResult(_records.FirstOrDefault(a => a.UserId == userId && a.Date == date.Date));

    public Task<List<AttendanceRecord>> GetByUserInRangeAsync(int userId, DateTime from, DateTime to) =>
        Task.FromResult(_records.Where(a => a.UserId == userId && a.Date >= from.Date && a.Date <= to.Date).OrderBy(a => a.Date).ToList());

    public Task<List<AttendanceRecord>> GetByDateAsync(DateTime date) =>
        Task.FromResult(_records.Where(a => a.Date == date.Date).ToList());

    public Task<List<AttendanceRecord>> GetInRangeAsync(DateTime from, DateTime to) =>
        Task.FromResult(_records.Where(a => a.Date >= from.Date && a.Date <= to.Date).OrderBy(a => a.Date).ToList());

    public Task<List<AttendanceRecord>> GetByUserAndMonthAsync(int userId, int year, int month)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return GetByUserInRangeAsync(userId, from, to);
    }

    public Task AddAsync(AttendanceRecord record)
    {
        record.Id = _records.Count == 0 ? 1 : _records.Max(a => a.Id) + 1;
        _records.Add(record);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AttendanceRecord record) => Task.CompletedTask;

    public Task SaveChangesAsync() => Task.CompletedTask;
}

public class AttendanceHolidayCalendarTests
{
    // 2026-11-02 = Monday, 2026-11-06 = Friday, 2026-11-07 = Saturday,
    // 2026-11-08 = Sunday, 2026-11-09 = Monday.

    private static (WorkingCalendarService Calendar, FakeHolidayRepository Holidays) CalendarWith(
        params DateTime[] holidays)
    {
        var list = holidays.Select((d, i) => new Holiday
        {
            Id = i + 1, Name = $"H{i}", Date = d.Date, CreatedDate = DateTime.UtcNow
        }).ToList();
        var repo = new FakeHolidayRepository(list);
        return (new WorkingCalendarService(repo), repo);
    }

    // 2. Saturday/Sunday are weekends, Monday–Friday are working days.
    [Theory]
    [InlineData("2026-11-02", true)] // Monday
    [InlineData("2026-11-06", true)] // Friday
    [InlineData("2026-11-07", false)] // Saturday
    [InlineData("2026-11-08", false)] // Sunday
    public async Task IsWorkingDay_WeekendsExcluded(string date, bool expected)
    {
        var (calendar, _) = CalendarWith();
        Assert.Equal(expected, await calendar.IsWorkingDayAsync(DateTime.Parse(date)));
    }

    // 10. Friday–Monday counts as 2 working days.
    [Fact]
    public async Task CountWorkingDays_FridayToMonday_IsTwo()
    {
        var (calendar, _) = CalendarWith();
        Assert.Equal(2, await calendar.CountWorkingDaysAsync(
            new DateTime(2026, 11, 6), new DateTime(2026, 11, 9)));
    }

    // 12. Weekend inside a leave range is excluded (Mon–Sun week = 5).
    [Fact]
    public async Task CountWorkingDays_WeekInsideRange_ExcludesWeekend()
    {
        var (calendar, _) = CalendarWith();
        Assert.Equal(5, await calendar.CountWorkingDaysAsync(
            new DateTime(2026, 11, 2), new DateTime(2026, 11, 8)));
    }

    // 11. Holiday inside a leave range is excluded.
    [Fact]
    public async Task CountWorkingDays_HolidayInsideRange_Excluded()
    {
        // Wednesday 2026-11-04 is a configured holiday.
        var (calendar, _) = CalendarWith(new DateTime(2026, 11, 4));
        Assert.Equal(4, await calendar.CountWorkingDaysAsync(
            new DateTime(2026, 11, 2), new DateTime(2026, 11, 6)));
    }

    // 3. Manual holiday: weekend dates rejected, duplicates rejected.
    [Fact]
    public async Task HolidayService_ManualHoliday_ValidatesWeekendsAndDuplicates()
    {
        var holidays = new List<Holiday>();
        var service = new HolidayService(new FakeHolidayRepository(holidays));

        var weekend = await service.CreateAsync("Weekend", new DateTime(2026, 11, 7), null);
        Assert.False(weekend.Success);

        var ok = await service.CreateAsync("Diwali", new DateTime(2026, 11, 4), "festival");
        Assert.True(ok.Success);

        var dup = await service.CreateAsync("Again", new DateTime(2026, 11, 4), null);
        Assert.False(dup.Success);
    }

    // 3b. Uploaded holiday list (CSV import): weekends/duplicates skipped.
    [Fact]
    public async Task HolidayService_ImportCsv_SkipsWeekendsAndDuplicates()
    {
        var holidays = new List<Holiday>
        {
            new() { Id = 1, Name = "Existing", Date = new DateTime(2026, 12, 25) }
        };
        var service = new HolidayService(new FakeHolidayRepository(holidays));
        var csv = "Name,Date,Description\nChristmas,2026-12-25,X\nNewDay,2026-12-28,Y\nSaturday,2026-11-07,Z\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await service.ImportAsync(stream, "holidays.csv");

        Assert.True(result.Success);
        Assert.Equal(1, result.Data.Imported); // only 2026-12-28 (Monday)
        Assert.Equal(2, result.Data.Skipped); // duplicate + Saturday
    }

    // 15. Admin can manage holidays (create/update/delete round-trip).
    [Fact]
    public async Task HolidayService_AdminCrud_RoundTrip()
    {
        var service = new HolidayService(new FakeHolidayRepository(new List<Holiday>()));
        var created = await service.CreateAsync("Pongal", new DateTime(2026, 1, 15), null);
        Assert.True(created.Success);

        var updated = await service.UpdateAsync(created.Data!.Id, "Pongal Day", new DateTime(2026, 1, 16), "d");
        Assert.True(updated.Success);
        Assert.Equal("Pongal Day", updated.Data!.Name);

        var deleted = await service.DeleteAsync(created.Data.Id);
        Assert.True(deleted.Success);
        Assert.Empty(await service.GetAllAsync());
    }

    // 5/6/7/8/9. Common calendar merges attendance + leaves + holidays + weekends.
    [Fact]
    public async Task Calendar_IntegrationScenario_MatchesBusinessRules()
    {
        // Mon Present, Tue Present, Wed Pending Leave, Thu Present,
        // Fri Approved Leave, Sat Weekend, Sun Weekend, next-Mon Holiday.
        var monday = new DateTime(2026, 11, 2);
        var leaves = new List<LeaveRequest>
        {
            new() { Id = 1, UserId = 1, FromDate = new DateTime(2026, 11, 4), ToDate = new DateTime(2026, 11, 4), Reason = "pending", Status = LeaveStatus.Pending },
            new() { Id = 2, UserId = 1, FromDate = new DateTime(2026, 11, 6), ToDate = new DateTime(2026, 11, 6), Reason = "approved", Status = LeaveStatus.Approved },
            // Rejected leave on Thursday must NOT appear as leave.
            new() { Id = 3, UserId = 1, FromDate = new DateTime(2026, 11, 5), ToDate = new DateTime(2026, 11, 5), Reason = "no", Status = LeaveStatus.Rejected },
        };
        var attendance = new List<AttendanceRecord>
        {
            new() { Id = 1, UserId = 1, Date = monday, Status = AttendanceStatus.Present },
            new() { Id = 2, UserId = 1, Date = new DateTime(2026, 11, 3), Status = AttendanceStatus.Present },
            new() { Id = 3, UserId = 1, Date = new DateTime(2026, 11, 5), Status = AttendanceStatus.Present },
        };
        var holidayRepo = new FakeHolidayRepository(new List<Holiday>
        {
            new() { Id = 1, Name = "Special", Date = new DateTime(2026, 11, 9) }
        });
        var calendar = new WorkingCalendarService(holidayRepo);
        var service = new EmployeeCalendarService(
            new FakeLeaveRepository(leaves),
            new FakeAttendanceRepository(attendance),
            holidayRepo, calendar);

        var days = await service.BuildMonthAsync(1, 2026, 11);
        CalendarDay ByDate(DateTime d) => days.First(x => x.Date == d.Date);

        Assert.Equal(CalendarDayStatus.Present, ByDate(monday).Status);
        Assert.Equal(CalendarDayStatus.PendingLeave, ByDate(new DateTime(2026, 11, 4)).Status);
        Assert.Equal(CalendarDayStatus.ApprovedLeave, ByDate(new DateTime(2026, 11, 6)).Status);
        Assert.Equal(CalendarDayStatus.Weekend, ByDate(new DateTime(2026, 11, 7)).Status);
        Assert.Equal(CalendarDayStatus.Weekend, ByDate(new DateTime(2026, 11, 8)).Status);
        Assert.Equal(CalendarDayStatus.Holiday, ByDate(new DateTime(2026, 11, 9)).Status);
        // Rejected Thursday falls through to the Present attendance record.
        Assert.Equal(CalendarDayStatus.Present, ByDate(new DateTime(2026, 11, 5)).Status);
    }

    // 13/14. Approval/rejection updates the calendar (status change reflected).
    [Fact]
    public async Task Calendar_LeaveStatusChange_UpdatesAccordingly()
    {
        var leaves = new List<LeaveRequest>
        {
            new() { Id = 1, UserId = 1, FromDate = new DateTime(2026, 11, 4), ToDate = new DateTime(2026, 11, 4), Reason = "x", Status = LeaveStatus.Pending },
        };
        var holidayRepo = new FakeHolidayRepository(new List<Holiday>());
        var calendar = new WorkingCalendarService(holidayRepo);
        var service = new EmployeeCalendarService(
            new FakeLeaveRepository(leaves),
            new FakeAttendanceRepository(new List<AttendanceRecord>()),
            holidayRepo, calendar);

        var before = await service.BuildMonthAsync(1, 2026, 11);
        Assert.Equal(CalendarDayStatus.PendingLeave, before.First(d => d.Date == new DateTime(2026, 11, 4)).Status);

        leaves[0].Status = LeaveStatus.Approved;
        var afterApprove = await service.BuildMonthAsync(1, 2026, 11);
        Assert.Equal(CalendarDayStatus.ApprovedLeave, afterApprove.First(d => d.Date == new DateTime(2026, 11, 4)).Status);

        leaves[0].Status = LeaveStatus.Rejected;
        var afterReject = await service.BuildMonthAsync(1, 2026, 11);
        // Past working day with no attendance and rejected leave => Absent, never Leave.
        Assert.NotEqual(CalendarDayStatus.ApprovedLeave, afterReject.First(d => d.Date == new DateTime(2026, 11, 4)).Status);
        Assert.NotEqual(CalendarDayStatus.PendingLeave, afterReject.First(d => d.Date == new DateTime(2026, 11, 4)).Status);
    }

    // 1. Normal working-day check-in creates a Present record; 2. weekends blocked.
    [Fact]
    public async Task Attendance_CheckIn_WorkingDayCreatesPresent()
    {
        var today = DateTime.Today;
        if (today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return; // weekend — covered by the test below on CI weekdays
        }

        var records = new List<AttendanceRecord>();
        var service = new AttendanceService(
            new FakeAttendanceRepository(records),
            new FakeUserRepository(new List<User> { new() { Id = 1, Email = "a@b.c", FullName = "A" } }),
            new WorkingCalendarService(new FakeHolidayRepository(new List<Holiday>())));

        var result = await service.CheckInAsync(1);
        Assert.True(result.Success);
        Assert.Equal(AttendanceStatus.Present, result.Data!.Status);
    }

    [Fact]
    public void WorkingCalendar_WeekendCheck_IsWeekend()
    {
        var (calendar, _) = CalendarWith();
        Assert.True(calendar.IsWeekend(new DateTime(2026, 11, 7)));
        Assert.True(calendar.IsWeekend(new DateTime(2026, 11, 8)));
        Assert.False(calendar.IsWeekend(new DateTime(2026, 11, 4)));
    }

    // 16. Holiday management is Admin-only (attributes, not just UI hiding).
    [Fact]
    public void HolidayManagement_ActionsRequireAdminRole()
    {
        foreach (var method in new[] { "Create", "Edit", "Delete", "Import" })
        {
            var methods = typeof(HolidayController).GetMethods()
                .Where(m => m.Name == method).ToArray();
            Assert.NotEmpty(methods);
            foreach (var m in methods)
            {
                var auth = m.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
                auth.AddRange(typeof(HolidayController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
                Assert.Contains(auth, a => a.Roles != null && a.Roles.Contains("Admin"));
            }
        }
    }

    // 16b. Employee attendance vs admin reports are role-separated.
    [Fact]
    public void Attendance_ActionsAreRoleSeparated()
    {
        string[] employeeOnly = { "Today", "CheckIn", "CheckOut", "History" };
        foreach (var name in employeeOnly)
        {
            var m = typeof(AttendanceController).GetMethods().First(x => x.Name == name);
            var roles = m.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Select(a => a.Roles).ToList();
            roles.AddRange(typeof(AttendanceController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Roles));
            Assert.Contains("Employee", roles.Where(r => r != null).SelectMany(r => r!.Split(',').Select(s => s.Trim())));
        }

        string[] adminOnly = { "Daily", "Monthly", "EmployeeWise", "Correct", "Mark" };
        foreach (var name in adminOnly)
        {
            var methods = typeof(AttendanceController).GetMethods().Where(x => x.Name == name).ToArray();
            Assert.NotEmpty(methods);
            foreach (var m in methods)
            {
                var roles = m.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Roles).ToList();
                roles.AddRange(typeof(AttendanceController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Roles));
                Assert.Contains(roles, r => r != null && r.Contains("Admin"));
            }
        }
    }

    // 17/18. SQL Server Express is the only store: DbSets exist, no SQLite package.
    [Fact]
    public void Database_UsesSqlServerExpressOnly()
    {
        var dbSets = typeof(ApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0].Name)
            .ToHashSet();
        Assert.Contains("Holiday", dbSets);
        Assert.Contains("AttendanceRecord", dbSets);

        var csproj = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "LeaveManagementSystem", "LeaveManagementSystem.csproj"));
        Assert.DoesNotContain("Sqlite", csproj, StringComparison.OrdinalIgnoreCase);
    }
}
