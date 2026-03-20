using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Globalization;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.Shifts;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public List<Shift> Shifts { get; set; } = new();
    public List<SelectListItem> Departments { get; set; } = new();
    public string CurrentWeek { get; set; } = string.Empty;

    public async Task OnGetAsync(string? week, Guid? departmentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        // Sæt standard uge (denne uge) - korrekt ISO uge format
        if (string.IsNullOrEmpty(week))
        {
            var today = DateTime.Today;
            var year = today.Year;
            var weekNumber = ISOWeek.GetWeekOfYear(today);
            CurrentWeek = $"{year}-W{weekNumber:D2}";
        }
        else
        {
            CurrentWeek = week;
        }

        // Parse ugenummer til datoer
        var (startDate, endDate) = ParseWeek(CurrentWeek);

        // Hent afdelinger til dropdown
        Departments = await _context.Departments
            .Where(d => d.TenantId == user.TenantId && d.IsActive)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        // Hent vagter
        var query = _context.Shifts
            .Include(s => s.Department)
            .Include(s => s.Employee)
            .Where(s => s.TenantId == user.TenantId
                && s.StartTime >= startDate
                && s.StartTime <= endDate);

        if (departmentId.HasValue)
        {
            query = query.Where(s => s.DepartmentId == departmentId.Value);
        }

        Shifts = await query
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    private static (DateTime start, DateTime end) ParseWeek(string? weekString)
    {
        // Hvis ugen er tom eller ugyldig, brug denne uge
        if (string.IsNullOrWhiteSpace(weekString))
        {
            return GetCurrentWeek();
        }

        try
        {
            // Format: 2025-W12 eller 2025-W01
            if (!weekString.Contains("-W"))
            {
                return GetCurrentWeek();
            }

            var parts = weekString.Split("-W");
            if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var weekNumber))
            {
                return GetCurrentWeek();
            }

            // Brug ISOWeek til at få korrekte datoer
            var startDate = ISOWeek.ToDateTime(year, weekNumber, DayOfWeek.Monday);
            var endDate = startDate.AddDays(6).AddHours(23).AddMinutes(59);

            return (startDate, endDate);
        }
        catch
        {
            return GetCurrentWeek();
        }
    }

    private static (DateTime start, DateTime end) GetCurrentWeek()
    {
        var today = DateTime.Today;
        var year = today.Year;
        var weekNumber = ISOWeek.GetWeekOfYear(today);
        var startDate = ISOWeek.ToDateTime(year, weekNumber, DayOfWeek.Monday);
        var endDate = startDate.AddDays(6).AddHours(23).AddMinutes(59);

        return (startDate, endDate);
    }
}