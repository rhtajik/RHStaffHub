using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
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

        // Sæt standard uge (denne uge)
        if (string.IsNullOrEmpty(week))
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            CurrentWeek = startOfWeek.ToString("yyyy-'W'ww");
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

    private static (DateTime start, DateTime end) ParseWeek(string weekString)
    {
        // Format: 2025-W12
        var parts = weekString.Split('-');
        if (parts.Length != 2 || !parts[0].StartsWith("20") || !parts[1].StartsWith("W"))
        {
            var today = DateTime.Today;
            var start = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            return (start, start.AddDays(6));
        }

        var year = int.Parse(parts[0]);
        var week = int.Parse(parts[1].Substring(1));

        // Beregn første mandag i ugen
        var jan1 = new DateTime(year, 1, 1);
        var daysOffset = DayOfWeek.Monday - jan1.DayOfWeek;
        var firstMonday = jan1.AddDays(daysOffset);
        var startDate = firstMonday.AddDays((week - 1) * 7);

        return (startDate, startDate.AddDays(6));
    }
}