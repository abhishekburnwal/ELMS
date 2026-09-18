using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.ViewModels;

public class EmployeeFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
    [DisplayName("Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    // Required when creating (enforced in EmployeeService); optional on edit —
    // leaving it blank keeps the existing password.
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 4, ErrorMessage = "Password must be at least 4 characters.")]
    public string? Password { get; set; }

    [Range(0, 60, ErrorMessage = "Leave balance must be between 0 and 60 days.")]
    [DisplayName("Annual Leave Balance (days)")]
    public int LeaveBalance { get; set; } = 20;
}
