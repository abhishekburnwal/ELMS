using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.ViewModels;

namespace LeaveManagementSystem.Services.Interfaces;

public interface IEmployeeService
{
    Task<List<User>> GetEmployeesAsync(string? search);

    Task<User?> GetByIdAsync(int id);

    Task<ServiceResult<User>> CreateAsync(EmployeeFormViewModel model);

    Task<ServiceResult<User>> UpdateAsync(EmployeeFormViewModel model);

    Task<ServiceResult<User>> DeactivateAsync(int id);

    Task<ServiceResult<User>> ReactivateAsync(int id);

    // DATABASE.md §6: Used = SUM(ToDate-FromDate+1) over Approved; Remaining = Balance - Used.
    Task<(int Used, int Remaining)> GetLeaveUsageAsync(int userId);
}
