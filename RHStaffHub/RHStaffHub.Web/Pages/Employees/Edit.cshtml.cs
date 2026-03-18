using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.Employees;

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
    public ApplicationUser Employee { get; set; } = new();

    public List<SelectListItem> Roles { get; set; } = new();
    public List<SelectListItem> Departments { get; set; } = new();
    public bool IsAdmin { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToPage("/Account/Login");

        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        if (currentUser == null) return RedirectToPage("/Account/Login");

        IsAdmin = User.IsInRole("Admin");

        Employee = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == currentUser.TenantId);

        if (Employee == null)
            return NotFound();

        // Manager må ikke redigere Admin
        if (!IsAdmin && Employee.Role == "Admin")
        {
            return Forbid();
        }

        // Hent roller
        Roles = new List<SelectListItem>
        {
            new SelectListItem { Value = "Employee", Text = "Medarbejder" },
            new SelectListItem { Value = "Manager", Text = "Manager" }
        };

        if (IsAdmin)
        {
            Roles.Insert(0, new SelectListItem { Value = "Admin", Text = "Administrator" });
        }

        // Hent afdelinger
        Departments = await _context.Departments
            .Where(d => d.TenantId == currentUser.TenantId && d.IsActive)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToPage("/Account/Login");

        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        if (currentUser == null) return RedirectToPage("/Account/Login");

        IsAdmin = User.IsInRole("Admin");

        // Hent eksisterende bruger
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == Employee.Id && u.TenantId == currentUser.TenantId);

        if (existingUser == null)
            return NotFound();

        // Manager må ikke redigere Admin
        if (!IsAdmin && existingUser.Role == "Admin")
        {
            return Forbid();
        }

        // Valider rolleændring
        if (!IsAdmin && Employee.Role == "Admin")
        {
            ModelState.AddModelError("", "Du har ikke tilladelse til at sætte rollen til Administrator");
            await LoadDropdowns(currentUser.TenantId);
            return Page();
        }

        // Opdater felter
        existingUser.FirstName = Employee.FirstName;
        existingUser.LastName = Employee.LastName;
        existingUser.PhoneNumber = Employee.PhoneNumber;
        existingUser.EmployeeNumber = Employee.EmployeeNumber;
        existingUser.PrimaryDepartmentId = Employee.PrimaryDepartmentId;
        existingUser.HourlyRate = Employee.HourlyRate;
        existingUser.IsActive = Employee.IsActive;

        // Opdater rolle hvis den er ændret
        if (existingUser.Role != Employee.Role)
        {
            await _userManager.RemoveFromRoleAsync(existingUser, existingUser.Role);
            await _userManager.AddToRoleAsync(existingUser, Employee.Role);
            existingUser.Role = Employee.Role;
        }

        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToPage("/Account/Login");

        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        if (currentUser == null) return RedirectToPage("/Account/Login");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == Employee.Id && u.TenantId == currentUser.TenantId);

        if (user == null)
            return NotFound();

        // Generer ny midlertidig adgangskode
        var newPassword = "TempPass123!";
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            // I en rigtig app: Send email til brugeren med den nye adgangskode
            TempData["Message"] = $"Adgangskode nulstillet. Midlertidig adgangskode: {newPassword}";
        }

        return RedirectToPage("./Index");
    }

    private async Task LoadDropdowns(string tenantId)
    {
        Roles = new List<SelectListItem>
        {
            new SelectListItem { Value = "Employee", Text = "Medarbejder" },
            new SelectListItem { Value = "Manager", Text = "Manager" }
        };

        if (IsAdmin)
        {
            Roles.Insert(0, new SelectListItem { Value = "Admin", Text = "Administrator" });
        }

        Departments = await _context.Departments
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();
    }
}