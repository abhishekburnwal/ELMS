using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LeaveManagementSystem.ViewModels.Validation;

// ARCHITECTURE.md §5 — model-level check that ToDate >= FromDate.
// The overlap check stays in LeaveService since it needs a database lookup.
[AttributeUsage(AttributeTargets.Property)]
public class DateRangeAttribute : ValidationAttribute
{
    private readonly string _fromPropertyName;

    public DateRangeAttribute(string fromPropertyName)
    {
        _fromPropertyName = fromPropertyName;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateTime toDate)
        {
            return ValidationResult.Success;
        }

        var fromProperty = validationContext.ObjectType.GetProperty(_fromPropertyName);
        if (fromProperty?.GetValue(validationContext.ObjectInstance) is not DateTime fromDate)
        {
            return ValidationResult.Success;
        }

        return toDate.Date >= fromDate.Date
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} cannot be earlier than {_fromPropertyName}.");
    }
}
