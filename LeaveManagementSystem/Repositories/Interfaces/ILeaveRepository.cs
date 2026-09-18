using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Repositories.Interfaces;

public interface ILeaveRepository
{
    Task<LeaveRequest?> GetByIdAsync(int id);

    Task<List<LeaveRequest>> GetByUserAsync(int userId);

    Task<List<LeaveRequest>> GetAllAsync(LeaveStatus? status = null);

    // ELMS-18 — status/date/employee filters for the admin view + export.
    Task<List<LeaveRequest>> GetFilteredAsync(LeaveStatus? status, DateTime? from, DateTime? to, string? search);

    Task AddAsync(LeaveRequest request);

    Task UpdateAsync(LeaveRequest request);

    Task SaveChangesAsync();

    Task<bool> HasOverlapAsync(int userId, DateTime from, DateTime to, int? excludeId = null);

    // Approved requests for a set of users in one query (used/remaining computation).
    Task<List<LeaveRequest>> GetApprovedForUsersAsync(IEnumerable<int> userIds);

    // Tracks an audit entry on the shared DbContext (saved by SaveChanges).
    Task AddAuditAsync(AuditLog entry);
}
