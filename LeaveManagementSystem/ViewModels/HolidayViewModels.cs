using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.ViewModels;

public class HolidayFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Holiday name is required.")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Date is required.")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }
}

public class HolidayListViewModel
{
    public int Year { get; set; }

    public List<Models.Entities.Holiday> Holidays { get; set; } = new();
}
