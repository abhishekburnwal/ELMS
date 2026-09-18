using LeaveManagementSystem.Hubs;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace LeaveManagementSystem.Tests;

public sealed class FakeClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> Sent { get; } = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Sent.Add((method, args));
        return Task.CompletedTask;
    }

    public ValueTask<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<T>(default!);
}

public sealed class FakeHubClients : IHubClients
{
    public FakeClientProxy Single { get; } = new();

    public IClientProxy All => Single;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Single;
    public IClientProxy Client(string connectionId) => Single;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Single;
    public IClientProxy Group(string groupName) => Single;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Single;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => Single;
    public IClientProxy User(string userId) => Single;
    public IClientProxy Users(IReadOnlyList<string> userIds) => Single;
}

public sealed class FakeGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class FakeHubContext : IHubContext<NotificationHub>
{
    public FakeHubClients FakeClients { get; } = new();

    public IHubClients Clients => FakeClients;

    public IGroupManager Groups { get; } = new FakeGroupManager();
}

public sealed class FakeAudit : IAuditService
{
    private readonly FakeLeaveRepository _repo;

    public FakeAudit(FakeLeaveRepository repo) => _repo = repo;

    public Task RecordDecisionAsync(int leaveRequestId, int actionByUserId, string action) =>
        _repo.AddAuditAsync(new AuditLog
        {
            LeaveRequestId = leaveRequestId,
            ActionByUserId = actionByUserId,
            Action = action,
            ActionDate = DateTime.UtcNow
        });
}
