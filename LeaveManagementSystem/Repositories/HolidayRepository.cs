using LeaveManagementSystem.Data;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Repositories;

public class HolidayRepository : IHolidayRepository
{
    private readonly ApplicationDbContext _context;

    public HolidayRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Holiday?> GetByIdAsync(int id) =>
        _context.Holidays.FirstOrDefaultAsync(h => h.Id == id);

    public Task<List<Holiday>> GetByYearAsync(int year) =>
        _context.Holidays
            .Where(h => h.Date.Year == year)
            .OrderBy(h => h.Date)
            .ToListAsync();

    public Task<List<Holiday>> GetAllAsync() =>
        _context.Holidays.OrderBy(h => h.Date).ToListAsync();

    public Task<List<Holiday>> GetInRangeAsync(DateTime from, DateTime to)
    {
        var f = from.Date;
        var t = to.Date;
        return _context.Holidays
            .Where(h => h.Date >= f && h.Date <= t)
            .OrderBy(h => h.Date)
            .ToListAsync();
    }

    public async Task<HashSet<DateTime>> GetDateSetInRangeAsync(DateTime from, DateTime to)
    {
        var list = await GetInRangeAsync(from, to);
        return list.Select(h => h.Date.Date).ToHashSet();
    }

    public Task<bool> ExistsOnDateAsync(DateTime date, int? excludeId = null)
    {
        var d = date.Date;
        return _context.Holidays.AnyAsync(h =>
            h.Date == d && (excludeId == null || h.Id != excludeId.Value));
    }

    public async Task AddAsync(Holiday holiday)
    {
        await _context.Holidays.AddAsync(holiday);
    }

    public Task UpdateAsync(Holiday holiday)
    {
        _context.Holidays.Update(holiday);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Holiday holiday)
    {
        _context.Holidays.Remove(holiday);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
