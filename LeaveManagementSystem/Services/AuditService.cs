using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.Services;

public class AuditService : IAuditService
{
    private readonly ILeaveRepository _leaves;

    public AuditService(ILeaveRepository leaves)
    {
        _leaves = leaves;
    }

    public async Task RecordDecisionAsync(int leaveRequestId, int actionByUserId, string action)
    {
        await _leaves.AddAuditAsync(new AuditLog
        {
            LeaveRequestId = leaveRequestId,
            ActionByUserId = actionByUserId,
            Action = action,
            ActionDate = DateTime.UtcNow
        });
    }

    public async Task RecordAttendanceChangeAsync(
        int? attendanceRecordId, int actionByUserId, string action, string? details)
    {
        await _leaves.AddAuditAsync(new AuditLog
        {
            AttendanceRecordId = attendanceRecordId,
            ActionByUserId = actionByUserId,
            Action = action,
            Details = string.IsNullOrWhiteSpace(details)
                ? null
                : details.Trim().Length > 500 ? details.Trim()[..500] : details.Trim(),
            ActionDate = DateTime.UtcNow
        });
    }
}
