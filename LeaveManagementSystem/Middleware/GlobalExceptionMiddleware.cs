using System.Diagnostics;
using System.Security.Claims;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.Middleware;

// ELMS-21 — centralized catch-all for unhandled request exceptions. Register
// AFTER UseExceptionHandler in Program.cs (deliberately): the exception-handler
// middleware must stay outermost so it can still render the generic /Home/Error
// page; this middleware sits inside it, records the full details (ILogger +
// ExceptionLogs table), then rethrows so the existing error-page flow — and its
// guarantee of never leaking exception details — is unchanged. No controller
// needs its own try/catch.
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await LogAsync(context, ex);
            throw;
        }
    }

    private async Task LogAsync(HttpContext context, Exception ex)
    {
        int? userId = null;
        if (int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed))
        {
            userId = parsed;
        }

        var entry = new ExceptionLog
        {
            Timestamp = DateTime.UtcNow,
            HttpMethod = Truncate(context.Request.Method, 10)!,
            Path = Truncate(context.Request.Path.ToString() + context.Request.QueryString.ToString(), 1000)!,
            UserId = userId,
            Username = Truncate(context.User.FindFirstValue(ClaimTypes.Name), 150),
            ExceptionType = Truncate(ex.GetType().FullName ?? ex.GetType().Name, 200)!,
            Message = Truncate(ex.Message, 2000)!,
            StackTrace = ex.StackTrace,
            // Same expression as HomeController.Error's RequestId, so the
            // reference shown on the generic error page matches this row.
            CorrelationId = Truncate(Activity.Current?.Id ?? context.TraceIdentifier, 100)!
        };

        // Existing ILogger pipeline (providers/levels per appsettings) — the
        // exception instance carries the stack trace; the message carries the
        // searchable fields. Info/Warning logging elsewhere is untouched.
        _logger.LogError(ex,
            "Unhandled {HttpMethod} {Path} correlation {CorrelationId} user {UserId}/{Username} {ExceptionType}: {Message}",
            entry.HttpMethod, entry.Path, entry.CorrelationId,
            entry.UserId?.ToString() ?? "-", entry.Username ?? "-",
            entry.ExceptionType, entry.Message);

        // Durable copy in SQL Server via the existing DbContext. A fresh scope
        // is required (middleware is a singleton); if persistence itself fails
        // (e.g. the database is what's down), fall back to ILogger only —
        // logging must never break the error path.
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IExceptionLogStore>();
            await store.SaveAsync(entry);
        }
        catch (Exception storeEx)
        {
            _logger.LogWarning(storeEx,
                "Could not persist exception log for correlation {CorrelationId}.",
                entry.CorrelationId);
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
