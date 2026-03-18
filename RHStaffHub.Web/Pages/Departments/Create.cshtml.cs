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
    public Department Department { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        // Hent virksomhed
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.TenantId == user.TenantId);

        if (company == null)
        {
            ModelState.AddModelError("", "Ingen virksomhed fundet");
            return Page();
        }

        // Sæt værdier
        Department.TenantId = user.TenantId;
        Department.CompanyId = company.Id;
        Department.IsActive = true;

        _context.Departments.Add(Department);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}