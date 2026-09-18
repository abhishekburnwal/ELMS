namespace LeaveManagementSystem.ViewModels;

// One row of Admin/Employees, with leave usage computed on read (DATABASE.md §6).
public class EmployeeListItemViewModel
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int LeaveBalance { get; set; }

    public int UsedDays { get; set; }

    public int RemainingDays { get; set; }
}
