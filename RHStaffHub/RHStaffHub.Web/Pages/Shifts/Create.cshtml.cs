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
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [BindProperty]
    public Shift Shift { get; set; } = new();

    [BindProperty]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [BindProperty]
    public TimeSpan StartTime { get; set; } = new TimeSpan(8, 0, 0); // 08:00

    [BindProperty]
    public TimeSpan EndTime { get; set; } = new TimeSpan(16, 0, 0); // 16:00

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> Employees { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        // Hent afdelinger
        Departments = await _context.Departments
            .Where(d => d.TenantId == user.TenantId && d.IsActive)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        // Hent medarbejdere
        Employees = await _context.Users
            .Where(u => u.TenantId == user.TenantId && u.IsActive && u.Role != "Admin")
            .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.FullName })
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        // Validering
        if (Shift.DepartmentId == Guid.Empty)
        {
            ModelState.AddModelError("Shift.DepartmentId", "Vælg en afdeling");
        }

        if (EndTime <= StartTime)
        {
            ModelState.AddModelError("EndTime", "Slut tid skal være efter start tid");
        }

        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        // Sæt start og slut tidspunkter
        Shift.StartTime = StartDate.Date + StartTime;
        Shift.EndTime = StartDate.Date + EndTime;

        // Hvis slut er før start (nattevagt), tilføj en dag
        if (Shift.EndTime <= Shift.StartTime)
        {
            Shift.EndTime = Shift.EndTime.AddDays(1);
        }

        Shift.TenantId = user.TenantId;
        Shift.Status = Shift.EmployeeId.HasValue ? "Confirmed" : "Scheduled";

        _context.Shifts.Add(Shift);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}