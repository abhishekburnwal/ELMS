using LeaveManagementSystem.Models.Entities;

namespace LeaveManagementSystem.Services.Interfaces;

// Records who approved/rejected a request and when (ELMS-19).
// The entry is tracked on the shared DbContext and saved by the caller's
// SaveChanges, so it lands in the same transaction as the status update.
public interface IAuditService
{
    Task RecordDecisionAsync(int leaveRequestId, int actionByUserId, string action);
}
