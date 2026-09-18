using LeaveManagementSystem.Models.Entities;

namespace LeaveManagementSystem.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);

    Task<User?> GetByEmailAsync(string email);

    Task<List<User>> GetEmployeesAsync(string? search);

    Task<bool> EmailExistsAsync(string email, int? excludeUserId = null);

    Task AddAsync(User user);

    Task UpdateAsync(User user);

    Task SaveChangesAsync();
}
