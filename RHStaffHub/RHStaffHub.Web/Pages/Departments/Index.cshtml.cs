using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.Departments;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public List<Department> Departments { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        // FILTRER: Kun aktive afdelinger
        Departments = await _context.Departments
            .Where(d => d.TenantId == user.TenantId && d.IsActive == true)  // <-- TILFØJET: && d.IsActive == true
            .OrderBy(d => d.Name)
            .ToListAsync();
    }
}