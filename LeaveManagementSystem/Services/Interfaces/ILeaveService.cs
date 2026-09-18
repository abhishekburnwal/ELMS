using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Services.Interfaces;

public interface ILeaveService
{
    Task<ServiceResult<LeaveRequest>> SubmitRequestAsync(int userId, DateTime fromDate, DateTime toDate, string reason);

    Task<List<LeaveRequest>> GetHistoryAsync(int userId);

    Task<List<LeaveRequest>> GetAllAsync(LeaveStatus? status = null);

    Task<List<LeaveRequest>> GetFilteredAsync(LeaveStatus? status, DateTime? from, DateTime? to, string? search);

    Task<ServiceResult<LeaveRequest>> UpdateStatusAsync(int requestId, LeaveStatus newStatus, int reviewerId, string? remarks);
}
