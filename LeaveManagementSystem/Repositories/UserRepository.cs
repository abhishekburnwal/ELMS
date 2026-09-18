using LeaveManagementSystem.Data;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(int id) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByEmailAsync(string email) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email);

    public Task<List<User>> GetEmployeesAsync(string? search)
    {
        IQueryable<User> query = _context.Users
            .Where(u => u.Role == UserRole.Employee)
            .OrderBy(u => u.FullName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.FullName.Contains(term) || u.Email.Contains(term))
                .OrderBy(u => u.FullName);
        }

        return query.ToListAsync();
    }

    public Task<bool> EmailExistsAsync(string email, int? excludeUserId = null) =>
        _context.Users.AnyAsync(u =>
            u.Email == email && (!excludeUserId.HasValue || u.Id != excludeUserId.Value));

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
