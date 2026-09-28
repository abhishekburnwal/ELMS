using LeaveManagementSystem.Models.Entities;

namespace LeaveManagementSystem.Services.Interfaces;

public interface IHolidayService
{
    Task<List<Holiday>> GetByYearAsync(int year);

    Task<List<Holiday>> GetAllAsync();

    Task<Holiday?> GetByIdAsync(int id);

    Task<ServiceResult<Holiday>> CreateAsync(string name, DateTime date, string? description);

    Task<ServiceResult<Holiday>> UpdateAsync(int id, string name, DateTime date, string? description);

    Task<ServiceResult<bool>> DeleteAsync(int id);

    // Import from CSV or Excel stream. Returns (imported, skipped) counts.
    // CSV: Name,Date(yyyy-MM-dd),Description? — header row required.
    // Excel (.xlsx): same columns in the first worksheet.
    Task<ServiceResult<(int Imported, int Skipped)>> ImportAsync(Stream content, string fileName);

    Task<byte[]> ExportExcelAsync(int? year);
}
