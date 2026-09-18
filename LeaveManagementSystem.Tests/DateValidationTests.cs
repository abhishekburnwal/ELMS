using System.ComponentModel.DataAnnotations;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Services;
using LeaveManagementSystem.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeaveManagementSystem.Tests;

public class DateValidationTests
{
    private static LeaveService SubmitService(FakeLeaveRepository leaves) =>
        new(leaves, new FakeHubContext(), NullLogger<LeaveService>.Instance, new FakeAudit(leaves));

    private static bool TryValidate(object model, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    }

    [Fact]
    public void ApplyLeaveModel_PastFromDate_IsInvalid()
    {
        var model = new ApplyLeaveViewModel
        {
            FromDate = DateTime.Today.AddDays(-1),
            ToDate = DateTime.Today,
            Reason = "past"
        };

        Assert.False(TryValidate(model, out var results));
        Assert.Contains(results, r => r.ErrorMessage!.Contains("past"));
    }

    [Fact]
    public void ApplyLeaveModel_TodayAndFuture_AreValid()
    {
        var today = new ApplyLeaveViewModel
        {
            FromDate = DateTime.Today, ToDate = DateTime.Today, Reason = "x"
        };
        var future = new ApplyLeaveViewModel
        {
            FromDate = DateTime.Today.AddDays(3), ToDate = DateTime.Today.AddDays(5), Reason = "x"
        };

        Assert.True(TryValidate(today, out _));
        Assert.True(TryValidate(future, out _));
    }

    [Fact]
    public async Task SubmitRequestAsync_PastFromDate_FailsWithoutInsert()
    {
        var leaves = new FakeLeaveRepository(new List<LeaveRequest>());

        var result = await SubmitService(leaves).SubmitRequestAsync(
            1, DateTime.Today.AddDays(-2), DateTime.Today, "backdated");

        Assert.False(result.Success);
        Assert.Contains("past", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await leaves.GetByUserAsync(1));
    }

    [Fact]
    public async Task SubmitRequestAsync_TodaySameDay_Succeeds()
    {
        var leaves = new FakeLeaveRepository(new List<LeaveRequest>());

        var result = await SubmitService(leaves).SubmitRequestAsync(
            1, DateTime.Today, DateTime.Today, "today");

        Assert.True(result.Success);
    }
}
