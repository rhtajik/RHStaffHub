using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.ComponentModel.DataAnnotations;
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

    // Separate properties i stedet for at binde til Shift objektet
    [BindProperty]
    [Required(ErrorMessage = "Vælg en afdeling")]
    public Guid? DepartmentId { get; set; }

    [BindProperty]
    public Guid? EmployeeId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Vælg en dato")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [BindProperty]
    [Required(ErrorMessage = "Vælg start tid")]
    public TimeSpan StartTime { get; set; } = new TimeSpan(8, 0, 0);

    [BindProperty]
    [Required(ErrorMessage = "Vælg slut tid")]
    public TimeSpan EndTime { get; set; } = new TimeSpan(16, 0, 0);

    [BindProperty]
    public string? Title { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> Employees { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        await LoadDropdowns(user.TenantId);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        // Manuel validering
        if (!DepartmentId.HasValue || DepartmentId.Value == Guid.Empty)
        {
            ModelState.AddModelError("DepartmentId", "Vælg en afdeling");
        }

        if (EndTime <= StartTime)
        {
            ModelState.AddModelError("EndTime", "Slut tid skal være efter start tid");
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdowns(user.TenantId);
            return Page();
        }

        // Opret vagt objekt manuelt
        var shift = new Shift
        {
            DepartmentId = DepartmentId!.Value,
            EmployeeId = EmployeeId,
            StartTime = StartDate.Date + StartTime,
            EndTime = StartDate.Date + EndTime,
            Title = Title,
            Notes = Notes,
            TenantId = user.TenantId,
            Status = EmployeeId.HasValue ? "Confirmed" : "Scheduled"
        };

        // Hvis nattevagt
        if (shift.EndTime <= shift.StartTime)
        {
            shift.EndTime = shift.EndTime.AddDays(1);
        }

        _context.Shifts.Add(shift);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    private async Task LoadDropdowns(string tenantId)
    {
        Departments = await _context.Departments
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        Employees = await _context.Users
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Role != "Admin")
            .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.FullName })
            .ToListAsync();
    }
}