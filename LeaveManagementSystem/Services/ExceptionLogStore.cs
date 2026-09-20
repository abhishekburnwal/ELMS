using LeaveManagementSystem.Data;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.Services;

// ELMS-21 — EF Core implementation: one ExceptionLogs row per unhandled
// exception, in the existing SQL Server Express database.
public class ExceptionLogStore : IExceptionLogStore
{
    private readonly ApplicationDbContext _db;

    public ExceptionLogStore(ApplicationDbContext db) => _db = db;

    public async Task SaveAsync(ExceptionLog entry, CancellationToken cancellationToken = default)
    {
        _db.ExceptionLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
