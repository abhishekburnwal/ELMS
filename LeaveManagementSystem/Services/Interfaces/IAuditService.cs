using LeaveManagementSystem.Models.Entities;

namespace LeaveManagementSystem.Services.Interfaces;

// Records who approved/rejected a request and when (ELMS-19), and who
// changed an attendance/timesheet record and what changed (Phase 10).
// Entries are tracked on the shared DbContext and saved by the caller's
// SaveChanges, so each lands in the same transaction as the change itself.
public interface IAuditService
{
    Task RecordDecisionAsync(int leaveRequestId, int actionByUserId, string action);

    Task RecordAttendanceChangeAsync(
        int? attendanceRecordId, int actionByUserId, string action, string? details);
}
