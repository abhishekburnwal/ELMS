using LeaveManagementSystem.Models.Entities;

namespace LeaveManagementSystem.Repositories.Interfaces;

public interface IHolidayRepository
{
    Task<Holiday?> GetByIdAsync(int id);

    Task<List<Holiday>> GetByYearAsync(int year);

    Task<List<Holiday>> GetAllAsync();

    Task<List<Holiday>> GetInRangeAsync(DateTime from, DateTime to);

    Task<HashSet<DateTime>> GetDateSetInRangeAsync(DateTime from, DateTime to);

    Task<bool> ExistsOnDateAsync(DateTime date, int? excludeId = null);

    Task AddAsync(Holiday holiday);

    Task UpdateAsync(Holiday holiday);

    Task DeleteAsync(Holiday holiday);

    Task SaveChangesAsync();
}
