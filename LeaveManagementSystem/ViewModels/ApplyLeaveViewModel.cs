using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using LeaveManagementSystem.ViewModels.Validation;

namespace LeaveManagementSystem.ViewModels;

public class ApplyLeaveViewModel
{
    [Required(ErrorMessage = "From Date is required.")]
    [DataType(DataType.Date)]
    [DisplayName("From Date")]
    [NoPastDate(ErrorMessage = "From Date cannot be in the past.")]
    public DateTime FromDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "To Date is required.")]
    [DataType(DataType.Date)]
    [DisplayName("To Date")]
    [DateRange("FromDate", ErrorMessage = "To Date cannot be earlier than From Date.")]
    public DateTime ToDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "A reason is required.")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
