using LeaveManagementSystem.Data;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Repositories;

public class LeaveRepository : ILeaveRepository
{
    private readonly ApplicationDbContext _context;

    public LeaveRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<LeaveRequest?> GetByIdAsync(int id) =>
        _context.LeaveRequests
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Id == id);

    public Task<List<LeaveRequest>> GetByUserAsync(int userId) =>
        _context.LeaveRequests
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.AppliedDate)
            .ToListAsync();

    public Task<List<LeaveRequest>> GetAllAsync(LeaveStatus? status = null)
    {
        IQueryable<LeaveRequest> query = _context.LeaveRequests
            .Include(l => l.User)
            .Include(l => l.ReviewedBy)
            .OrderByDescending(l => l.AppliedDate);

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value)
                .OrderByDescending(l => l.AppliedDate);
        }

        return query.ToListAsync();
    }

    public Task<List<LeaveRequest>> GetFilteredAsync(
        LeaveStatus? status, DateTime? from, DateTime? to, string? search)
    {
        IQueryable<LeaveRequest> query = _context.LeaveRequests
            .Include(l => l.User)
            .Include(l => l.ReviewedBy)
            .OrderByDescending(l => l.AppliedDate);

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(l => l.FromDate >= from.Value.Date);
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.ToDate <= to.Value.Date);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                l.User!.FullName.Contains(term) ||
                l.User!.Email.Contains(term) ||
                l.Reason.Contains(term));
        }

        return query.OrderByDescending(l => l.AppliedDate).ToListAsync();
    }

    public async Task AddAsync(LeaveRequest request)
    {
        await _context.LeaveRequests.AddAsync(request);
    }

    public Task UpdateAsync(LeaveRequest request)
    {
        _context.LeaveRequests.Update(request);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    // ARCHITECTURE.md §4 — overlap check (service-layer rule, not a DB constraint).
    public Task<bool> HasOverlapAsync(int userId, DateTime from, DateTime to, int? excludeId = null) =>
        _context.LeaveRequests.AnyAsync(r =>
            r.UserId == userId &&
            r.Status != LeaveStatus.Rejected &&
            (excludeId == null || r.Id != excludeId.Value) &&
            from <= r.ToDate && to >= r.FromDate);

    public Task<List<LeaveRequest>> GetApprovedForUsersAsync(IEnumerable<int> userIds) =>
        _context.LeaveRequests
            .Where(r => r.Status == LeaveStatus.Approved && userIds.Contains(r.UserId))
            .ToListAsync();

    public async Task AddAuditAsync(AuditLog entry)
    {
        await _context.AuditLogs.AddAsync(entry);
    }
}
