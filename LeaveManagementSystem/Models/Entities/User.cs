using LeaveManagementSystem.Models.Enums;

namespace LeaveManagementSystem.Models.Entities;

public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Employee;

    public bool IsActive { get; set; } = true;

    public int LeaveBalance { get; set; } = 20;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

    public ICollection<LeaveRequest> ReviewedRequests { get; set; } = new List<LeaveRequest>();
}
