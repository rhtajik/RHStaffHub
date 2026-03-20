using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.TimeEntries;

[Authorize]
public class MyTimeEntriesModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MyTimeEntriesModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public List<TimeEntry> TimeEntries { get; set; } = new();
    public List<SelectListItem> Departments { get; set; } = new();
    public TimeEntry? ActiveEntry { get; set; }

    public double MonthlyHours { get; set; }
    public decimal MonthlyWage { get; set; }
    public int ApprovedCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        await LoadData(user);
        return Page();
    }

    public async Task<IActionResult> OnPostClockInAsync(Guid departmentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        // Tjek om allerede clocket ind
        var existing = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.EmployeeId == user.Id && t.ClockOut == null);

        if (existing != null)
        {
            ModelState.AddModelError("", "Du er allerede clocket ind");
            await LoadData(user);
            return Page();
        }

        // Opret ny entry
        var entry = new TimeEntry
        {
            EmployeeId = user.Id,
            DepartmentId = departmentId,
            ClockIn = DateTime.Now,
            TenantId = user.TenantId,
            Status = "Pending"
        };

        _context.TimeEntries.Add(entry);
        await _context.SaveChangesAsync();

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClockOutAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        // Find aktiv entry
        var entry = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.EmployeeId == user.Id && t.ClockOut == null);

        if (entry == null)
        {
            ModelState.AddModelError("", "Ingen aktiv registrering fundet");
            await LoadData(user);
            return Page();
        }

        // Clock ud
        entry.ClockOut = DateTime.Now;

        // Beregn løn
        var hours = (entry.ClockOut.Value - entry.ClockIn - (entry.BreakDuration ?? TimeSpan.Zero)).TotalHours;
        entry.CalculatedWage = (decimal)hours * user.HourlyRate;

        await _context.SaveChangesAsync();

        return RedirectToPage();
    }

    private async Task LoadData(ApplicationUser user)
    {
        // Hent aktiv entry (hvis clocket ind)
        ActiveEntry = await _context.TimeEntries
            .Include(t => t.Department)
            .FirstOrDefaultAsync(t => t.EmployeeId == user.Id && t.ClockOut == null);

        // Hent departments til dropdown
        Departments = await _context.Departments
            .Where(d => d.TenantId == user.TenantId && d.IsActive)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        // Hent historik (sidste 30 dage)
        var fromDate = DateTime.Today.AddDays(-30);
        TimeEntries = await _context.TimeEntries
            .Include(t => t.Department)
            .Where(t => t.EmployeeId == user.Id && t.ClockIn >= fromDate)
            .OrderByDescending(t => t.ClockIn)
            .ToListAsync();

        // Beregn månedens statistik
        var firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthEntries = TimeEntries
            .Where(t => t.ClockIn >= firstDayOfMonth && t.ClockOut.HasValue && t.Status == "Approved")
            .ToList();

        MonthlyHours = monthEntries.Sum(t =>
            (t.ClockOut.Value - t.ClockIn - (t.BreakDuration ?? TimeSpan.Zero)).TotalHours);
        MonthlyWage = monthEntries.Sum(t => t.CalculatedWage ?? 0);
        ApprovedCount = monthEntries.Count;
    }
}