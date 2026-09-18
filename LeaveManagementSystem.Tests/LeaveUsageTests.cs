using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeaveManagementSystem.Tests;

public class LeaveUsageTests
{
    private static (EmployeeService Service, List<LeaveRequest> Requests) CreateService()
    {
        var user = new User
        {
            Id = 1, FullName = "Test Employee", Email = "test@example.com",
            Role = UserRole.Employee, IsActive = true, LeaveBalance = 20
        };
        var requests = new List<LeaveRequest>
        {
            // Mon 2026-11-02 -> Fri 2026-11-06: 5 working days.
            new() { Id = 1, UserId = 1, FromDate = new DateTime(2026, 11, 2), ToDate = new DateTime(2026, 11, 6), Reason = "week", Status = LeaveStatus.Approved },
            // Sat -> Sun weekend only: 0 working days.
            new() { Id = 2, UserId = 1, FromDate = new DateTime(2026, 11, 7), ToDate = new DateTime(2026, 11, 8), Reason = "weekend", Status = LeaveStatus.Approved },
            // Fri 2026-11-06 -> Mon 2026-11-09: 2 working days.
            new() { Id = 3, UserId = 1, FromDate = new DateTime(2026, 11, 6), ToDate = new DateTime(2026, 11, 9), Reason = "long weekend", Status = LeaveStatus.Approved },
            // Pending must not count toward usage.
            new() { Id = 4, UserId = 1, FromDate = new DateTime(2026, 12, 1), ToDate = new DateTime(2026, 12, 5), Reason = "pending", Status = LeaveStatus.Pending },
        };
        var service = new EmployeeService(
            new FakeUserRepository(new List<User> { user }),
            new FakeLeaveRepository(requests));
        return (service, requests);
    }

    [Fact]
    public async Task GetLeaveUsageAsync_UsesWorkingDays_NotCalendarDays()
    {
        var (service, _) = CreateService();

        // Calendar-day math would give 5 + 2 + 4 = 11; working days give 5 + 0 + 2 = 7.
        var (used, remaining) = await service.GetLeaveUsageAsync(1);

        Assert.Equal(7, used);
        Assert.Equal(13, remaining); // 20 - 7
    }

    [Fact]
    public async Task SubmitRequestAsync_ValidRange_CreatesPendingRequest()
    {
        var (service, _) = CreateService();
        var leaves = new FakeLeaveRepository(new List<LeaveRequest>());
        var submitter = new LeaveService(leaves,
            new FakeHubContext(), NullLogger<LeaveService>.Instance, new FakeAudit(leaves));

        var result = await submitter.SubmitRequestAsync(
            9, new DateTime(2026, 11, 6), new DateTime(2026, 11, 9), "long weekend");

        Assert.True(result.Success);
        Assert.Equal(LeaveStatus.Pending, result.Data!.Status);
    }

    [Fact]
    public async Task SubmitRequestAsync_OverlappingRange_FailsBeforeDatabase()
    {
        var (_, requests) = CreateService();
        var leaves = new FakeLeaveRepository(requests);
        var submitter = new LeaveService(leaves,
            new FakeHubContext(), NullLogger<LeaveService>.Instance, new FakeAudit(leaves));

        // Overlaps the approved Mon->Fri request above.
        var result = await submitter.SubmitRequestAsync(
            1, new DateTime(2026, 11, 4), new DateTime(2026, 11, 5), "overlap");

        Assert.False(result.Success);
        Assert.DoesNotContain("overlap", requests
            .Where(l => l.Reason == "overlap")
            .Select(l => l.Reason));
    }

    [Fact]
    public async Task SubmitRequestAsync_ToBeforeFrom_Fails()
    {
        var leaves = new FakeLeaveRepository(new List<LeaveRequest>());
        var submitter = new LeaveService(leaves,
            new FakeHubContext(), NullLogger<LeaveService>.Instance, new FakeAudit(leaves));

        var result = await submitter.SubmitRequestAsync(
            1, new DateTime(2026, 11, 10), new DateTime(2026, 11, 9), "bad range");

        Assert.False(result.Success);
    }
}
