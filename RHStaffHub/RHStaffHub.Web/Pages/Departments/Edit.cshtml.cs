using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.Departments;

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
    public Department Department { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        Department = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == user.TenantId);

        if (Department == null)
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        // Sikkerhedstjek - tilhører afdelingen denne tenant?
        var existing = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == Department.Id && d.TenantId == user.TenantId);

        if (existing == null)
            return NotFound();

        // Opdater felter
        existing.Name = Department.Name;
        existing.Description = Department.Description;
        existing.Address = Department.Address;
        existing.LocationId = Department.LocationId;
        existing.IsActive = Department.IsActive;
        existing.GpsLatitude = Department.GpsLatitude;
        existing.GpsLongitude = Department.GpsLongitude;
        existing.GpsRadiusMeters = Department.GpsRadiusMeters;

        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        var existing = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == Department.Id && d.TenantId == user.TenantId);

        if (existing == null)
            return NotFound();

        // Soft delete
        existing.IsActive = false;
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}