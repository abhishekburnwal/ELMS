using LeaveManagementSystem.Services.Interfaces;
using LeaveManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagementSystem.Controllers;

[Authorize]
public class HolidayController : Controller
{
    private readonly IHolidayService _holidays;

    public HolidayController(IHolidayService holidays)
    {
        _holidays = holidays;
    }

    // Both roles can view the holiday calendar; management stays Admin-only.
    [HttpGet]
    public async Task<IActionResult> Index(int? year)
    {
        var y = year ?? DateTime.Today.Year;
        y = Math.Clamp(y, 2000, 2100);
        return View(new HolidayListViewModel
        {
            Year = y,
            Holidays = await _holidays.GetByYearAsync(y)
        });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View("Form", new HolidayFormViewModel());

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HolidayFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var result = await _holidays.CreateAsync(model.Name, model.Date, model.Description);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.ErrorMessage ?? "Could not create the holiday.");
            return View("Form", model);
        }

        TempData["Message"] = $"Holiday '{result.Data!.Name}' added.";
        return RedirectToAction(nameof(Index), new { year = result.Data.Date.Year });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var holiday = await _holidays.GetByIdAsync(id);
        if (holiday is null)
        {
            return NotFound();
        }

        return View("Form", new HolidayFormViewModel
        {
            Id = holiday.Id,
            Name = holiday.Name,
            Date = holiday.Date,
            Description = holiday.Description
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(HolidayFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var result = await _holidays.UpdateAsync(model.Id!.Value, model.Name, model.Date, model.Description);
        if (!result.Success)
        {
            ModelState.AddModelError(result.Field ?? string.Empty, result.ErrorMessage ?? "Could not update the holiday.");
            return View("Form", model);
        }

        TempData["Message"] = $"Holiday '{result.Data!.Name}' updated.";
        return RedirectToAction(nameof(Index), new { year = result.Data.Date.Year });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _holidays.DeleteAsync(id);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
        }
        else
        {
            TempData["Message"] = "Holiday deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Choose a CSV or Excel (.xlsx) file to import.";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".csv" and not ".xlsx")
        {
            TempData["Error"] = "Only .csv and .xlsx files are supported.";
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["Error"] = "File is too large (max 5 MB).";
            return RedirectToAction(nameof(Index));
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;
        var result = await _holidays.ImportAsync(stream, file.FileName);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
        }
        else
        {
            TempData["Message"] = $"Import complete: {result.Data.Imported} added, {result.Data.Skipped} skipped.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Export(int? year)
    {
        var bytes = await _holidays.ExportExcelAsync(year);
        var name = year.HasValue ? $"Holidays-{year}.xlsx" : "Holidays-All.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }
}
