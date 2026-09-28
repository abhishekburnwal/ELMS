using System.ComponentModel.DataAnnotations;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.ViewModels;

public class AttendanceTodayViewModel
{
    public AttendanceRecord? Today { get; set; }

    public bool IsWeekend { get; set; }

    public bool IsHoliday { get; set; }
}

public class AttendanceHistoryViewModel
{
    public List<AttendanceRecord> Records { get; set; } = new();
}

public class AttendanceMonthViewModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    public List<AttendanceRecord> Records { get; set; } = new();

    public Dictionary<DateTime, AttendanceRecord> ByDate { get; set; } = new();

    // Phase 9 — interactive calendar state: configured holidays in view,
    // whether today can be marked (working day + unmarked), and the popup form.
    // Phase 10 — LeaveByDate maps approved/pending leave days ("Leave"/"Pending")
    // so they never render as Absent.
    public HashSet<DateTime> Holidays { get; set; } = new();

    public Dictionary<DateTime, string> LeaveByDate { get; set; } = new();

    public bool CanMarkToday { get; set; }

    public SubmitAttendanceViewModel SubmitForm { get; set; } = new();
}

// Phase 9 — employee calendar popup: Date is rendered read-only and
// re-validated server-side (must equal today); hours + project mandatory.
public class SubmitAttendanceViewModel
{
    [Required]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Working hours are required.")]
    [Range(0.5, 24, ErrorMessage = "Working hours must be between 0.5 and 24.")]
    public decimal? WorkingHours { get; set; }

    [Required(ErrorMessage = "Project name is required.")]
    [StringLength(200, ErrorMessage = "Project name cannot exceed 200 characters.")]
    public string? ProjectName { get; set; }

    [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters.")]
    public string? Remarks { get; set; }
}

public class AdminDailyAttendanceViewModel
{
    public DateTime Date { get; set; }

    public List<AttendanceRecord> Records { get; set; } = new();
}

public class AdminMonthlyAttendanceViewModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    public List<AttendanceRecord> Records { get; set; } = new();
}

public class AdminEmployeeWiseViewModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string? Search { get; set; }

    public List<(User User, List<AttendanceRecord> Records)> Rows { get; set; } = new();
}

public class AttendanceCorrectionViewModel
{
    public int RecordId { get; set; }

    public int UserId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public AttendanceStatus Status { get; set; }

    [Range(0.5, 24, ErrorMessage = "Working hours must be between 0.5 and 24.")]
    public decimal? WorkingHours { get; set; }

    [StringLength(200, ErrorMessage = "Project name cannot exceed 200 characters.")]
    public string? ProjectName { get; set; }

    public string? Remarks { get; set; }
}

public class AttendanceUpsertViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Select an employee.")]
    public int UserId { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    [Range(0.5, 24, ErrorMessage = "Working hours must be between 0.5 and 24.")]
    public decimal? WorkingHours { get; set; }

    [StringLength(200, ErrorMessage = "Project name cannot exceed 200 characters.")]
    public string? ProjectName { get; set; }

    public string? Remarks { get; set; }
}

public class AdminAttendanceReportViewModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string? Search { get; set; }

    public List<AttendanceRecord> Records { get; set; } = new();

    public List<AdminAttendanceReportRow> Totals { get; set; } = new();
}

public class AdminAttendanceReportRow
{
    public int UserId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int DaysPresent { get; set; }

    public decimal TotalHours { get; set; }

    public List<string> Projects { get; set; } = new();
}

public class EmployeeCalendarViewModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    public List<CalendarDay> Days { get; set; } = new();

    public Dictionary<string, int> Summary { get; set; } = new();

    // Phase 9 — admin employee-wise calendar: employee selector state.
    public int? SelectedUserId { get; set; }

    public List<User> Employees { get; set; } = new();

    // Per-day attendance details for the click-to-view modal (hours/project).
    public Dictionary<DateTime, AttendanceRecord> AttendanceByDate { get; set; } = new();
}
