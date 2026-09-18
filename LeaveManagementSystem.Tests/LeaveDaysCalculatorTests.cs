using LeaveManagementSystem.Services;

namespace LeaveManagementSystem.Tests;

public class LeaveDaysCalculatorTests
{
    // Dates verified against the real 2026 calendar:
    // 2026-11-02 = Monday, 2026-11-04 = Wednesday, 2026-11-06 = Friday,
    // 2026-11-07 = Saturday, 2026-11-08 = Sunday, 2026-11-09 = Monday.
    [Theory]
    [InlineData("2026-11-06", "2026-11-09", 2)] // Friday -> Monday
    [InlineData("2026-11-02", "2026-11-06", 5)] // Monday -> Friday
    [InlineData("2026-11-06", "2026-11-08", 1)] // Friday -> Sunday
    [InlineData("2026-11-07", "2026-11-08", 0)] // Saturday -> Sunday
    [InlineData("2026-11-07", "2026-11-09", 1)] // Saturday -> Monday
    [InlineData("2026-11-08", "2026-11-09", 1)] // Sunday -> Monday
    [InlineData("2026-11-02", "2026-11-02", 1)] // Monday -> Monday
    [InlineData("2026-11-04", "2026-11-04", 1)] // Wednesday -> Wednesday
    [InlineData("2026-11-02", "2026-11-08", 5)] // Full Monday -> Sunday week
    public void CountWorkingDays_ReturnsExpected(string from, string to, int expected)
    {
        Assert.Equal(
            expected,
            LeaveDaysCalculator.CountWorkingDays(
                DateTime.Parse(from), DateTime.Parse(to)));
    }

    [Fact]
    public void CountWorkingDays_FromAfterTo_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LeaveDaysCalculator.CountWorkingDays(
                new DateTime(2026, 11, 10), new DateTime(2026, 11, 09)));
    }
}
