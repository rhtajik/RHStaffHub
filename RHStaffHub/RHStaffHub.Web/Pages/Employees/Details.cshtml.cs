using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.Employees;

[Authorize(Roles = "Admin,Manager")]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public ApplicationUser Employee { get; set; } = new();
    public double MonthlyHours { get; set; }
    public decimal MonthlyWage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToPage("/Account/Login");

        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        if (currentUser == null) return RedirectToPage("/Account/Login");

        Employee = await _context.Users
            .Include(u => u.PrimaryDepartment)
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == currentUser.TenantId);

        if (Employee == null)
            return NotFound();

        // Beregn månedens statistik
        var firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var timeEntries = await _context.TimeEntries
            .Where(t => t.EmployeeId == Employee.Id
                && t.ClockIn >= firstDayOfMonth
                && t.ClockOut != null
                && t.Status == "Approved")
            .ToListAsync();

        MonthlyHours = timeEntries.Sum(t =>
            ((t.ClockOut.Value - t.ClockIn - (t.BreakDuration ?? TimeSpan.Zero)).TotalHours));

        MonthlyWage = timeEntries.Sum(t => t.CalculatedWage ?? 0);

        return Page();
    }
}