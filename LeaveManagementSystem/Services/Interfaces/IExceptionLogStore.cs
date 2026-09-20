using LeaveManagementSystem.Models.Entities;

namespace LeaveManagementSystem.Services.Interfaces;

// ELMS-21 — persistence seam for unhandled-exception records. The EF
// implementation writes to the ExceptionLogs table; tests substitute a fake.
public interface IExceptionLogStore
{
    Task SaveAsync(ExceptionLog entry, CancellationToken cancellationToken = default);
}
