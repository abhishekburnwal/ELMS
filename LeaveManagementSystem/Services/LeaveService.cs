using LeaveManagementSystem.Hubs;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LeaveManagementSystem.Services;

public class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leaves;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<LeaveService> _logger;
    private readonly IAuditService _audit;

    public LeaveService(
        ILeaveRepository leaves,
        IHubContext<NotificationHub> hub,
        ILogger<LeaveService> logger,
        IAuditService audit)
    {
        _leaves = leaves;
        _hub = hub;
        _logger = logger;
        _audit = audit;
    }

    public async Task<ServiceResult<LeaveRequest>> SubmitRequestAsync(
        int userId, DateTime fromDate, DateTime toDate, string reason)
    {
        if (fromDate.Date < DateTime.Today)
        {
            return ServiceResult<LeaveRequest>.Fail("From Date cannot be in the past.");
        }

        if (toDate.Date < fromDate.Date)
        {
            return ServiceResult<LeaveRequest>.Fail("To Date cannot be earlier than From Date.");
        }

        if (await _leaves.HasOverlapAsync(userId, fromDate.Date, toDate.Date))
        {
            return ServiceResult<LeaveRequest>.Fail(
                "These dates overlap with another leave request you already have.");
        }

        var request = new LeaveRequest
        {
            UserId = userId,
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            Reason = reason.Trim(),
            Status = LeaveStatus.Pending,
            AppliedDate = DateTime.UtcNow
        };

        await _leaves.AddAsync(request);
        await _leaves.SaveChangesAsync();

        // Notify online admins the moment a request is submitted.
        // Best-effort like the decision push below — never fails the submit.
        try
        {
            await _hub.Clients.Group("Admins").SendAsync(
                "LeaveSubmitted",
                new { requestId = request.Id });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR submit push for leave request {RequestId} failed.", request.Id);
        }

        return ServiceResult<LeaveRequest>.Ok(request);
    }

    public Task<List<LeaveRequest>> GetHistoryAsync(int userId) =>
        _leaves.GetByUserAsync(userId);

    public Task<List<LeaveRequest>> GetAllAsync(LeaveStatus? status = null) =>
        _leaves.GetAllAsync(status);

    public Task<List<LeaveRequest>> GetFilteredAsync(
        LeaveStatus? status, DateTime? from, DateTime? to, string? search) =>
        _leaves.GetFilteredAsync(status, from, to, search);

    public async Task<ServiceResult<LeaveRequest>> UpdateStatusAsync(
        int requestId, LeaveStatus newStatus, int reviewerId, string? remarks)
    {
        if (newStatus == LeaveStatus.Pending)
        {
            return ServiceResult<LeaveRequest>.Fail("A decided request cannot be moved back to Pending.");
        }

        var request = await _leaves.GetByIdAsync(requestId);
        if (request is null)
        {
            return ServiceResult<LeaveRequest>.Fail("Leave request not found.");
        }

        // Block re-decisions on an already-decided request (BACKLOG ELMS-11).
        if (request.Status != LeaveStatus.Pending)
        {
            return ServiceResult<LeaveRequest>.Fail(
                $"This request has already been {request.Status}. It cannot be decided again.");
        }

        request.Status = newStatus;
        request.ReviewedByUserId = reviewerId;
        request.ReviewedDate = DateTime.UtcNow;
        request.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();

        await _leaves.UpdateAsync(request);

        // ELMS-19 — audit row joins the same DbContext change tracker, so it
        // commits in the same transaction as the status update below.
        await _audit.RecordDecisionAsync(request.Id, reviewerId, newStatus.ToString());

        await _leaves.SaveChangesAsync();

        // ELMS-17 — notify the employee in real time. Best-effort: a push
        // failure must never roll back an already-saved decision.
        try
        {
            await _hub.Clients.User(request.UserId.ToString()).SendAsync(
                "LeaveStatusChanged",
                new { requestId = request.Id, status = request.Status.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR push for leave request {RequestId} failed.", request.Id);
        }

        return ServiceResult<LeaveRequest>.Ok(request);
    }
}
