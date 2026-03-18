using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public ApplicationUser? CurrentUser { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public bool HasActiveTimeEntry { get; set; }
    public TimeEntry? ActiveTimeEntry { get; set; }
    public List<Shift> TodaysShifts { get; set; } = new();
    public double MonthlyHours { get; set; }
    public decimal EstimatedWage { get; set; }
    public bool IsAdminOrManager => CurrentUser?.Role == "Admin" || CurrentUser?.Role == "Manager";

    [BindProperty]
    public Guid SelectedDepartmentId { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        CurrentUser = await _userManager.FindByIdAsync(userId);
        if (CurrentUser == null) return RedirectToPage("/Account/Login");

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.TenantId == CurrentUser.TenantId);
        CompanyName = company?.Name ?? "Ukendt virksomhed";

        ActiveTimeEntry = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.EmployeeId == CurrentUser.Id && t.ClockOut == null);
        HasActiveTimeEntry = ActiveTimeEntry != null;

        var today = DateTime.Today;
        TodaysShifts = await _context.Shifts
            .Include(s => s.Department)
            .Where(s => s.EmployeeId == CurrentUser.Id
                && s.StartTime.Date == today
                && s.Status != "Cancelled")
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        Departments = await _context.Departments
            .Where(d => d.TenantId == CurrentUser.TenantId)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
        var timeEntries = await _context.TimeEntries
            .Where(t => t.EmployeeId == CurrentUser.Id
                && t.ClockIn >= firstDayOfMonth
                && t.ClockOut != null)
            .ToListAsync();

        MonthlyHours = timeEntries.Sum(t =>
            ((t.ClockOut.Value - t.ClockIn - (t.BreakDuration ?? TimeSpan.Zero)).TotalHours));

        EstimatedWage = timeEntries.Sum(t => t.CalculatedWage ?? 0);

        return Page();
    }

    public async Task<IActionResult> OnPostClockInAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        var existing = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.EmployeeId == user.Id && t.ClockOut == null);

        if (existing != null)
        {
            ModelState.AddModelError("", "Du er allerede clocket ind");
            return await OnGetAsync();
        }

        var timeEntry = new TimeEntry
        {
            EmployeeId = user.Id,
            DepartmentId = SelectedDepartmentId,
            ClockIn = DateTime.Now,
            TenantId = user.TenantId,
            Status = "Pending"
        };

        _context.TimeEntries.Add(timeEntry);
        await _context.SaveChangesAsync();

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClockOutAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        var timeEntry = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.EmployeeId == user.Id && t.ClockOut == null);

        if (timeEntry == null)
        {
            ModelState.AddModelError("", "Ingen aktiv clock-in fundet");
            return await OnGetAsync();
        }

        timeEntry.ClockOut = DateTime.Now;

        var hours = (timeEntry.ClockOut.Value - timeEntry.ClockIn - (timeEntry.BreakDuration ?? TimeSpan.Zero)).TotalHours;
        timeEntry.CalculatedWage = (decimal)hours * user.HourlyRate;

        await _context.SaveChangesAsync();

        return RedirectToPage();
    }
}