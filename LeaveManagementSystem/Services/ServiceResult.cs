namespace LeaveManagementSystem.Services;

// Result object for expected failure modes (ARCHITECTURE.md §6):
// services return failures inline so controllers can re-render the form
// with a friendly message instead of hitting the generic error page.
public class ServiceResult<T>
{
    public bool Success { get; private set; }

    public T? Data { get; private set; }

    public string? ErrorMessage { get; private set; }

    // Optional model field the error belongs to, so controllers can surface
    // it as a field-level message (e.g. duplicate email) instead of a summary.
    public string? Field { get; private set; }

    public static ServiceResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static ServiceResult<T> Fail(string errorMessage, string? field = null) =>
        new() { Success = false, ErrorMessage = errorMessage, Field = field };
}
