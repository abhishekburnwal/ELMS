using ClosedXML.Excel;
using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Repositories.Interfaces;
using LeaveManagementSystem.Services.Interfaces;

namespace LeaveManagementSystem.Services;

public class HolidayService : IHolidayService
{
    private readonly IHolidayRepository _holidays;

    public HolidayService(IHolidayRepository holidays)
    {
        _holidays = holidays;
    }

    public Task<List<Holiday>> GetByYearAsync(int year) => _holidays.GetByYearAsync(year);

    public Task<List<Holiday>> GetAllAsync() => _holidays.GetAllAsync();

    public Task<Holiday?> GetByIdAsync(int id) => _holidays.GetByIdAsync(id);

    public async Task<ServiceResult<Holiday>> CreateAsync(string name, DateTime date, string? description)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<Holiday>.Fail("Holiday name is required.", nameof(name));
        }

        if (name.Length > 200)
        {
            return ServiceResult<Holiday>.Fail("Holiday name cannot exceed 200 characters.", nameof(name));
        }

        var day = date.Date;
        if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return ServiceResult<Holiday>.Fail("Saturday and Sunday are already weekends — no manual holiday needed.");
        }

        if (await _holidays.ExistsOnDateAsync(day))
        {
            return ServiceResult<Holiday>.Fail("A holiday already exists on this date.");
        }

        var holiday = new Holiday
        {
            Name = name,
            Date = day,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedDate = DateTime.UtcNow
        };

        await _holidays.AddAsync(holiday);
        await _holidays.SaveChangesAsync();
        return ServiceResult<Holiday>.Ok(holiday);
    }

    public async Task<ServiceResult<Holiday>> UpdateAsync(int id, string name, DateTime date, string? description)
    {
        var existing = await _holidays.GetByIdAsync(id);
        if (existing is null)
        {
            return ServiceResult<Holiday>.Fail("Holiday not found.");
        }

        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<Holiday>.Fail("Holiday name is required.", nameof(name));
        }

        if (name.Length > 200)
        {
            return ServiceResult<Holiday>.Fail("Holiday name cannot exceed 200 characters.", nameof(name));
        }

        var day = date.Date;
        if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return ServiceResult<Holiday>.Fail("Saturday and Sunday are already weekends — no manual holiday needed.");
        }

        if (await _holidays.ExistsOnDateAsync(day, id))
        {
            return ServiceResult<Holiday>.Fail("A holiday already exists on this date.");
        }

        existing.Name = name;
        existing.Date = day;
        existing.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        await _holidays.UpdateAsync(existing);
        await _holidays.SaveChangesAsync();
        return ServiceResult<Holiday>.Ok(existing);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var existing = await _holidays.GetByIdAsync(id);
        if (existing is null)
        {
            return ServiceResult<bool>.Fail("Holiday not found.");
        }

        await _holidays.DeleteAsync(existing);
        await _holidays.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<(int Imported, int Skipped)>> ImportAsync(Stream content, string fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        List<(string Name, DateTime Date, string? Description)> rows;
        try
        {
            rows = ext == ".xlsx"
                ? ReadExcel(content)
                : ReadCsv(content);
        }
        catch (Exception ex)
        {
            return ServiceResult<(int, int)>.Fail($"Could not read the file: {ex.Message}");
        }

        var imported = 0;
        var skipped = 0;
        foreach (var (name, date, description) in rows)
        {
            var day = date.Date;
            if (string.IsNullOrWhiteSpace(name) ||
                day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ||
                await _holidays.ExistsOnDateAsync(day))
            {
                skipped++;
                continue;
            }

            await _holidays.AddAsync(new Holiday
            {
                Name = name.Trim().Length > 200 ? name.Trim()[..200] : name.Trim(),
                Date = day,
                Description = string.IsNullOrWhiteSpace(description)
                    ? null
                    : description.Trim().Length > 500 ? description.Trim()[..500] : description.Trim(),
                CreatedDate = DateTime.UtcNow
            });
            imported++;
        }

        await _holidays.SaveChangesAsync();
        return ServiceResult<(int, int)>.Ok((imported, skipped));
    }

    public async Task<byte[]> ExportExcelAsync(int? year)
    {
        var rows = year.HasValue
            ? await _holidays.GetByYearAsync(year.Value)
            : await _holidays.GetAllAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Holidays");
        string[] headers = { "Name", "Date", "Year", "Description" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var h in rows)
        {
            sheet.Cell(row, 1).Value = h.Name;
            sheet.Cell(row, 2).Value = h.Date;
            sheet.Cell(row, 2).Style.DateFormat.Format = "yyyy-MM-dd";
            sheet.Cell(row, 3).Value = h.Date.Year;
            sheet.Cell(row, 4).Value = h.Description ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static List<(string Name, DateTime Date, string? Description)> ReadCsv(Stream content)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var result = new List<(string, DateTime, string?)>();
        var first = true;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (first)
            {
                first = false; // skip header row
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 2)
            {
                continue;
            }

            if (!DateTime.TryParse(parts[1].Trim(), out var date))
            {
                continue;
            }

            result.Add((parts[0].Trim(), date.Date, parts.Length > 2 ? parts[2].Trim() : null));
        }

        return result;
    }

    private static List<(string Name, DateTime Date, string? Description)> ReadExcel(Stream content)
    {
        var result = new List<(string, DateTime, string?)>();
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.First();
        var first = true;
        foreach (var row in sheet.RowsUsed())
        {
            if (first)
            {
                first = false; // skip header row
                continue;
            }

            var name = row.Cell(1).GetString().Trim();
            if (!row.Cell(2).TryGetValue(out DateTime date))
            {
                continue;
            }

            var description = row.Cell(3).GetString().Trim();
            result.Add((name, date.Date, string.IsNullOrWhiteSpace(description) ? null : description));
        }

        return result;
    }
}
