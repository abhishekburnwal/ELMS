using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;

namespace LeaveManagementSystem.Tests;

// In-memory fakes — no database, no SQLite, SQL Server Express untouched.
public sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users;

    public FakeUserRepository(List<User> users) => _users = users;

    public Task<User?> GetByIdAsync(int id) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email) =>
        Task.FromResult(_users.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<List<User>> GetEmployeesAsync(string? search)
    {
        var query = _users.Where(u => u.Role == UserRole.Employee);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        return Task.FromResult(query.OrderBy(u => u.FullName).ToList());
    }

    public Task<bool> EmailExistsAsync(string email, int? excludeUserId = null) =>
        Task.FromResult(_users.Any(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
            (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));

    public Task AddAsync(User user)
    {
        user.Id = _users.Count == 0 ? 1 : _users.Max(u => u.Id) + 1;
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user) => Task.CompletedTask;

    public Task SaveChangesAsync() => Task.CompletedTask;
}

public sealed class FakeLeaveRepository : ILeaveRepository
{
    private readonly List<LeaveRequest> _requests;

    public FakeLeaveRepository(List<LeaveRequest> requests) => _requests = requests;

    public Task<LeaveRequest?> GetByIdAsync(int id) =>
        Task.FromResult(_requests.FirstOrDefault(l => l.Id == id));

    public Task<List<LeaveRequest>> GetByUserAsync(int userId) =>
        Task.FromResult(_requests
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.AppliedDate)
            .ToList());

    public Task<List<LeaveRequest>> GetAllAsync(LeaveStatus? status = null) =>
        Task.FromResult(_requests
            .Where(l => !status.HasValue || l.Status == status.Value)
            .OrderByDescending(l => l.AppliedDate)
            .ToList());

    public Task<List<LeaveRequest>> GetFilteredAsync(
        LeaveStatus? status, DateTime? from, DateTime? to, string? search) =>
        Task.FromResult(_requests
            .Where(l => !status.HasValue || l.Status == status.Value)
            .Where(l => !from.HasValue || l.FromDate >= from.Value.Date)
            .Where(l => !to.HasValue || l.ToDate <= to.Value.Date)
            .OrderByDescending(l => l.AppliedDate)
            .ToList());

    public Task AddAsync(LeaveRequest request)
    {
        request.Id = _requests.Count == 0 ? 1 : _requests.Max(l => l.Id) + 1;
        _requests.Add(request);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LeaveRequest request) => Task.CompletedTask;

    public Task SaveChangesAsync() => Task.CompletedTask;

    public Task<bool> HasOverlapAsync(int userId, DateTime from, DateTime to, int? excludeId = null) =>
        Task.FromResult(_requests.Any(r =>
            r.UserId == userId &&
            r.Status != LeaveStatus.Rejected &&
            (excludeId == null || r.Id != excludeId.Value) &&
            from <= r.ToDate && to >= r.FromDate));

    public Task<List<LeaveRequest>> GetApprovedForUsersAsync(IEnumerable<int> userIds) =>
        Task.FromResult(_requests
            .Where(r => r.Status == LeaveStatus.Approved && userIds.Contains(r.UserId))
            .ToList());

    public readonly List<AuditLog> Audits = new();

    public Task AddAuditAsync(AuditLog entry)
    {
        Audits.Add(entry);
        return Task.CompletedTask;
    }
}
