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

namespace RHStaffHub.Web.Pages.Employees;

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
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> Roles { get; set; } = new();
    public List<SelectListItem> Departments { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Fornavn er påkrævet")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Efternavn er påkrævet")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email er påkrævet")]
        [EmailAddress(ErrorMessage = "Ugyldig email")]
        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }
        public string? EmployeeNumber { get; set; }

        [Required(ErrorMessage = "Rolle er påkrævet")]
        public string Role { get; set; } = "Employee";

        public Guid? PrimaryDepartmentId { get; set; }

        [Required(ErrorMessage = "Adgangskode er påkrævet")]
        [StringLength(100, ErrorMessage = "Adgangskoden skal være mindst {2} tegn lang.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public decimal HourlyRate { get; set; }
    }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        // Hent roller
        Roles = new List<SelectListItem>
        {
            new SelectListItem { Value = "Employee", Text = "Medarbejder" },
            new SelectListItem { Value = "Manager", Text = "Manager" }
        };

        // Kun Admin kan oprette andre Admin
        if (User.IsInRole("Admin"))
        {
            Roles.Insert(0, new SelectListItem { Value = "Admin", Text = "Administrator" });
        }

        // Hent afdelinger
        Departments = await _context.Departments
            .Where(d => d.TenantId == user.TenantId && d.IsActive)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        // Sæt standard timeløn
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.TenantId == user.TenantId);

        if (company != null)
        {
            Input.HourlyRate = company.StandardHourlyRate;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return RedirectToPage("/Account/Login");

        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        if (currentUser == null) return RedirectToPage("/Account/Login");

        // Valider at Manager ikke opretter Admin
        if (!User.IsInRole("Admin") && Input.Role == "Admin")
        {
            ModelState.AddModelError("", "Du har ikke tilladelse til at oprette administratorer");
        }

        if (!ModelState.IsValid)
        {
            // Genindlæs dropdowns
            await OnGetAsync();
            return Page();
        }

        // Opret bruger
        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            FirstName = Input.FirstName,
            LastName = Input.LastName,
            PhoneNumber = Input.PhoneNumber,
            EmployeeNumber = Input.EmployeeNumber,
            Role = Input.Role,
            TenantId = currentUser.TenantId,
            PrimaryDepartmentId = Input.PrimaryDepartmentId,
            HourlyRate = Input.HourlyRate,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            // Tilføj til rolle
            await _userManager.AddToRoleAsync(user, Input.Role);

            return RedirectToPage("./Index");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        await OnGetAsync();
        return Page();
    }
}