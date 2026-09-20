namespace LeaveManagementSystem.Models.Entities;

// One row per unhandled request exception, written by GlobalExceptionMiddleware
// (ELMS-21). Deliberately no FK to Users — anonymous requests and deactivated
// accounts must never block logging.
public class ExceptionLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string HttpMethod { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int? UserId { get; set; }

    public string? Username { get; set; }

    public string ExceptionType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? StackTrace { get; set; }

    // Activity id when present, else HttpContext.TraceIdentifier — matches the
    // reference shown on the generic error page (ErrorViewModel.RequestId).
    public string CorrelationId { get; set; } = string.Empty;
}
