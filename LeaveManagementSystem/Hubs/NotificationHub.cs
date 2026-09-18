using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LeaveManagementSystem.Hubs;

// Push channel for leave events (ARCHITECTURE.md §7, ELMS-17 + submit notify).
// Same-origin cookie auth applies; the user identifier is the NameIdentifier
// claim (their Users.Id) issued at login. Admins join the "Admins" group to
// receive LeaveSubmitted pushes the moment an employee applies.
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        await base.OnConnectedAsync();
    }
}
