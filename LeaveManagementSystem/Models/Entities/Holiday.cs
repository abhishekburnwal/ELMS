namespace LeaveManagementSystem.Models.Entities;

public class Holiday
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public int Year => Date.Year;

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
