using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.ComponentModel.DataAnnotations;

namespace RHStaffHub.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Virksomhedsnavn er påkrævet")]
        public string CompanyName { get; set; } = string.Empty; 

        [Required(ErrorMessage = "Fornavn er påkrævet")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Efternavn er påkrævet")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email er påkrævet")]
        [EmailAddress(ErrorMessage = "Ugyldig email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adgangskode er påkrævet")]
        [StringLength(100, ErrorMessage = "Adgangskoden skal være mindst {2} tegn lang.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Adgangskoderne matcher ikke")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Opret unik tenant ID
        var tenantId = Guid.NewGuid().ToString();

        // Opret virksomhed
        var company = new Company
        {
            Name = Input.CompanyName,
            TenantId = tenantId
        };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        // Opret admin bruger
        var user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            FirstName = Input.FirstName,
            LastName = Input.LastName,
            Role = "Admin",
            TenantId = tenantId,
            EmailConfirmed = true,
            HourlyRate = company.StandardHourlyRate
        };

        var result = await _userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            // Tilføj til Admin rolle
            await _userManager.AddToRoleAsync(user, "Admin");

            // Log brugeren ind
            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToPage("/Dashboard/Index");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}