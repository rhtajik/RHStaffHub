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

    // Separat property til Delete handler
    [BindProperty(Name = "Department.Id")]
    public Guid DeleteDepartmentId { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        Department = await _context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == user.TenantId);

        if (Department == null)
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Fjern ModelState fejl for navigation properties
        ModelState.Remove("Department.Company");
        ModelState.Remove("Department.Employees");
        ModelState.Remove("Department.Shifts");
        ModelState.Remove("Department.TimeEntries");

        if (!ModelState.IsValid)
        {
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
            {
                Console.WriteLine($"ModelError: {error.ErrorMessage}");
            }
            return Page();
        }

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
        Console.WriteLine($">>> DELETE STARTET - ID: {DeleteDepartmentId} <<<");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        // Find afdeling direkte på ID - BRUG AsNoTracking() = false (default)
        var existing = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == DeleteDepartmentId && d.TenantId == user.TenantId);

        if (existing == null)
        {
            Console.WriteLine(">>> AFDELING IKKE FUNDET <<<");
            return NotFound();
        }

        Console.WriteLine($">>> SLETTER: {existing.Name}, IsActive={existing.IsActive} <<<");

        // Soft delete
        existing.IsActive = false;

        // Explicit mark as modified
        _context.Entry(existing).State = EntityState.Modified;

        Console.WriteLine($">>> EF STATE: {_context.Entry(existing).State} <<<");

        var result = await _context.SaveChangesAsync();

        Console.WriteLine($">>> SAVE CHANGES RESULT: {result} rækker påvirket <<<");

        return RedirectToPage("./Index");
    }
}