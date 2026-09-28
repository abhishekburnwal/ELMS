using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Repositories.Interfaces;

public interface IAttendanceRepository
{
    Task<AttendanceRecord?> GetByIdAsync(int id);

    Task<AttendanceRecord?> GetByUserAndDateAsync(int userId, DateTime date);

    Task<List<AttendanceRecord>> GetByUserInRangeAsync(int userId, DateTime from, DateTime to);

    Task<List<AttendanceRecord>> GetByDateAsync(DateTime date);

    Task<List<AttendanceRecord>> GetInRangeAsync(DateTime from, DateTime to);

    Task<List<AttendanceRecord>> GetByUserAndMonthAsync(int userId, int year, int month);

    Task AddAsync(AttendanceRecord record);

    Task UpdateAsync(AttendanceRecord record);

    Task SaveChangesAsync();
}
