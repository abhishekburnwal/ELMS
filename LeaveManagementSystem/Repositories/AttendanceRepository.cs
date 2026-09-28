using LeaveManagementSystem.Data;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly ApplicationDbContext _context;

    public AttendanceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<AttendanceRecord?> GetByIdAsync(int id) =>
        _context.AttendanceRecords
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

    public Task<AttendanceRecord?> GetByUserAndDateAsync(int userId, DateTime date)
    {
        var d = date.Date;
        return _context.AttendanceRecords
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == d);
    }

    public Task<List<AttendanceRecord>> GetByUserInRangeAsync(int userId, DateTime from, DateTime to)
    {
        var f = from.Date;
        var t = to.Date;
        return _context.AttendanceRecords
            .Where(a => a.UserId == userId && a.Date >= f && a.Date <= t)
            .OrderBy(a => a.Date)
            .ToListAsync();
    }

    public Task<List<AttendanceRecord>> GetByDateAsync(DateTime date)
    {
        var d = date.Date;
        return _context.AttendanceRecords
            .Include(a => a.User)
            .Where(a => a.Date == d)
            .OrderBy(a => a.User!.FullName)
            .ToListAsync();
    }

    public Task<List<AttendanceRecord>> GetInRangeAsync(DateTime from, DateTime to)
    {
        var f = from.Date;
        var t = to.Date;
        return _context.AttendanceRecords
            .Include(a => a.User)
            .Where(a => a.Date >= f && a.Date <= t)
            .OrderBy(a => a.Date)
            .ToListAsync();
    }

    public Task<List<AttendanceRecord>> GetByUserAndMonthAsync(int userId, int year, int month)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return GetByUserInRangeAsync(userId, from, to);
    }

    public async Task AddAsync(AttendanceRecord record)
    {
        await _context.AttendanceRecords.AddAsync(record);
    }

    public Task UpdateAsync(AttendanceRecord record)
    {
        _context.AttendanceRecords.Update(record);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
