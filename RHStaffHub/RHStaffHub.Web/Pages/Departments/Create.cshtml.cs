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
        Console.WriteLine(">>> POST STARTET <<<");

        // Midlertidigt: Ignorer ModelState
        // if (!ModelState.IsValid)
        // {
        //     return Page();
        // }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            Console.WriteLine(">>> INGEN BRUGER <<<");
            return RedirectToPage("/Account/Login");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            Console.WriteLine(">>> BRUGER IKKE FUNDET <<<");
            return RedirectToPage("/Account/Login");
        }

        Console.WriteLine($"Bruger: {user.Email}, Tenant: {user.TenantId}");

        // Hent eller opret virksomhed
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.TenantId == user.TenantId);

        if (company == null)
        {
            Console.WriteLine(">>> OPPRETTER VIRKSOMHED <<<");
            company = new Company
            {
                Name = "Min Virksomhed",
                TenantId = user.TenantId
            };
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();
        }

        // Sæt værdier
        Department.TenantId = user.TenantId;
        Department.CompanyId = company.Id;
        Department.IsActive = true;

        Console.WriteLine($"Gemmer: {Department.Name}, {Department.Address}");

        _context.Departments.Add(Department);
        await _context.SaveChangesAsync();

        Console.WriteLine(">>> GEMT! <<<");

        return RedirectToPage("./Index");
    }
}