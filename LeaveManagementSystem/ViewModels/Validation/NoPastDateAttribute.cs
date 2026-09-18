using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.ViewModels.Validation;

// ARCHITECTURE.md §5 — model-level check that FromDate is today or later.
// Past-date leave is rejected here for field-level UX and again in
// LeaveService, which remains the backend source of truth.
[AttributeUsage(AttributeTargets.Property)]
public class NoPastDateAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateTime date)
        {
            return ValidationResult.Success;
        }

        return date.Date >= DateTime.Today
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage ?? "From Date cannot be in the past.");
    }
}
