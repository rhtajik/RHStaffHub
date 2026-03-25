using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.Employees;

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

    public List<ApplicationUser> Employees { get; set; } = new();
    public bool ShowInactive { get; set; }

    public async Task OnGetAsync(bool showInactive = false)
    {
        ShowInactive = showInactive;

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == null) return;

        var currentUser = await _userManager.FindByIdAsync(currentUserId);
        if (currentUser == null) return;

        // Filtrer baseret på showInactive parameter
        var query = _context.Users
            .Include(u => u.PrimaryDepartment)
            .Where(u => u.TenantId == currentUser.TenantId);

        if (!showInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        Employees = await query
            .OrderBy(u => u.LastName)
            .ToListAsync();
    }
}