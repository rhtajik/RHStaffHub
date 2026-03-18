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
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public EditModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [BindProperty]
    public Shift Shift { get; set; } = new();

    [BindProperty]
    public TimeSpan StartTime { get; set; }

    [BindProperty]
    public TimeSpan EndTime { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> Employees { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        Shift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == user.TenantId);

        if (Shift == null)
            return NotFound();

        StartTime = Shift.StartTime.TimeOfDay;
        EndTime = Shift.EndTime.TimeOfDay;

        await LoadDropdowns(user.TenantId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        if (EndTime <= StartTime)
        {
            ModelState.AddModelError("EndTime", "Slut tid skal være efter start tid");
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdowns(user.TenantId);
            return Page();
        }

        var existing = await _context.Shifts
            .FirstOrDefaultAsync(s => s.Id == Shift.Id && s.TenantId == user.TenantId);

        if (existing == null)
            return NotFound();

        // Opdater felter
        existing.DepartmentId = Shift.DepartmentId;
        existing.EmployeeId = Shift.EmployeeId;
        existing.Title = Shift.Title;
        existing.Notes = Shift.Notes;
        existing.Status = Shift.Status;

        // Opdater tider (behold samme dato, skift kun tid)
        var date = existing.StartTime.Date;
        existing.StartTime = date + StartTime;
        existing.EndTime = date + EndTime;

        if (existing.EndTime <= existing.StartTime)
        {
            existing.EndTime = existing.EndTime.AddDays(1);
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        var existing = await _context.Shifts
            .FirstOrDefaultAsync(s => s.Id == Shift.Id && s.TenantId == user.TenantId);

        if (existing == null)
            return NotFound();

        _context.Shifts.Remove(existing);
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