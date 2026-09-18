using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services.Interfaces;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace LeaveManagementSystem.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IUserRepository _users;
    private readonly ILeaveRepository _leaves;
    private readonly PasswordHasher<User> _hasher = new();

    public EmployeeService(IUserRepository users, ILeaveRepository leaves)
    {
        _users = users;
        _leaves = leaves;
    }

    public Task<List<User>> GetEmployeesAsync(string? search) =>
        _users.GetEmployeesAsync(search);

    public Task<User?> GetByIdAsync(int id) => _users.GetByIdAsync(id);

    public async Task<ServiceResult<User>> CreateAsync(EmployeeFormViewModel model)
    {
        if (await _users.EmailExistsAsync(model.Email))
        {
            return ServiceResult<User>.Fail(
                "An employee with this email already exists.", nameof(EmployeeFormViewModel.Email));
        }

        if (string.IsNullOrWhiteSpace(model.Password))
        {
            return ServiceResult<User>.Fail("A password is required for a new employee.");
        }

        var user = new User
        {
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim(),
            Role = UserRole.Employee,
            IsActive = true,
            LeaveBalance = model.LeaveBalance
        };
        user.PasswordHash = _hasher.HashPassword(user, model.Password);

        await _users.AddAsync(user);
        await _users.SaveChangesAsync();
        return ServiceResult<User>.Ok(user);
    }

    public async Task<ServiceResult<User>> UpdateAsync(EmployeeFormViewModel model)
    {
        if (!model.Id.HasValue)
        {
            return ServiceResult<User>.Fail("Employee id is missing.");
        }

        var user = await _users.GetByIdAsync(model.Id.Value);
        if (user is null)
        {
            return ServiceResult<User>.Fail("Employee not found.");
        }

        if (await _users.EmailExistsAsync(model.Email, model.Id))
        {
            return ServiceResult<User>.Fail(
                "An employee with this email already exists.", nameof(EmployeeFormViewModel.Email));
        }

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.LeaveBalance = model.LeaveBalance;

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            user.PasswordHash = _hasher.HashPassword(user, model.Password);
        }

        await _users.UpdateAsync(user);
        await _users.SaveChangesAsync();
        return ServiceResult<User>.Ok(user);
    }

    public async Task<ServiceResult<User>> DeactivateAsync(int id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null)
        {
            return ServiceResult<User>.Fail("Employee not found.");
        }

        // Soft delete only — leave history must survive (BACKLOG ELMS-08).
        user.IsActive = false;
        await _users.UpdateAsync(user);
        await _users.SaveChangesAsync();
        return ServiceResult<User>.Ok(user);
    }

    public async Task<ServiceResult<User>> ReactivateAsync(int id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null)
        {
            return ServiceResult<User>.Fail("Employee not found.");
        }

        user.IsActive = true;
        await _users.UpdateAsync(user);
        await _users.SaveChangesAsync();
        return ServiceResult<User>.Ok(user);
    }

    public async Task<(int Used, int Remaining)> GetLeaveUsageAsync(int userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            return (0, 0);
        }

        var history = await _leaves.GetByUserAsync(userId);
        var used = history
            .Where(l => l.Status == LeaveStatus.Approved)
            .Sum(l => LeaveDaysCalculator.CountWorkingDays(l.FromDate, l.ToDate));

        return (used, user.LeaveBalance - used);
    }
}
