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
public class AssignModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AssignModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public Shift Shift { get; set; } = new();

    [BindProperty]
    public Guid SelectedEmployeeId { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    public List<SelectListItem> AvailableEmployees { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        Shift = await _context.Shifts
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == user.TenantId);

        if (Shift == null)
            return NotFound();

        // Find tilgængelige medarbejdere (ikke allerede optaget på dette tidspunkt)
        var busyEmployeeIds = await _context.Shifts
            .Where(s => s.TenantId == user.TenantId
                && s.Id != id
                && s.Status != "Cancelled"
                && ((s.StartTime <= Shift.StartTime && s.EndTime > Shift.StartTime) ||
                    (s.StartTime < Shift.EndTime && s.EndTime >= Shift.EndTime) ||
                    (s.StartTime >= Shift.StartTime && s.EndTime <= Shift.EndTime)))
            .Select(s => s.EmployeeId)
            .Where(id => id != null)
            .Cast<Guid>()
            .ToListAsync();

        // Hent medarbejdere med deres primære afdeling
        var employees = await _context.Users
            .Include(u => u.PrimaryDepartment)  // <-- Tilføj denne linje
            .Where(u => u.TenantId == user.TenantId
                && u.IsActive
                && u.Role != "Admin"
                && !busyEmployeeIds.Contains(u.Id))
            .ToListAsync();

        AvailableEmployees = employees.Select(u => new SelectListItem
        {
            Value = u.Id.ToString(),
            Text = $"{u.FullName} ({u.PrimaryDepartment?.Name ?? "Ingen afd."})"
        }).ToList();

        // Tilføj nuværende tildelt hvis der er en
        if (Shift.EmployeeId.HasValue)
        {
            SelectedEmployeeId = Shift.EmployeeId.Value;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        var shift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == user.TenantId);

        if (shift == null)
            return NotFound();

        if (SelectedEmployeeId == Guid.Empty)
        {
            ModelState.AddModelError("SelectedEmployeeId", "Vælg en medarbejder");
            await OnGetAsync(id);
            return Page();
        }

        // Tjek at medarbejder eksisterer og tilhører samme tenant
        var employee = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == SelectedEmployeeId && u.TenantId == user.TenantId);

        if (employee == null)
        {
            ModelState.AddModelError("", "Medarbejder ikke fundet");
            await OnGetAsync(id);
            return Page();
        }

        // Tildel vagt
        shift.EmployeeId = SelectedEmployeeId;
        shift.Status = "Confirmed";

        if (!string.IsNullOrEmpty(Notes))
        {
            shift.Notes = string.IsNullOrEmpty(shift.Notes)
                ? Notes
                : $"{shift.Notes}\n\nBesked: {Notes}";
        }

        await _context.SaveChangesAsync();

        // I en rigtig app: Send notifikation til medarbejder her
        // await _notificationService.SendShiftAssignedNotification(employee, shift);

        return RedirectToPage("./Index");
    }
}