using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RHStaffHub.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace RHStaffHub.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Email er påkrævet")]
        [EmailAddress(ErrorMessage = "Ugyldig email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adgangskode er påkrævet")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Ugyldigt login");
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Kontoen er låst. Prøv igen senere.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "Ugyldigt login");
        return Page();
    }
}