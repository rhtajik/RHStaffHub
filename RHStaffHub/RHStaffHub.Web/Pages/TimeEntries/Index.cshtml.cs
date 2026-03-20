using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.TimeEntries;

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

    public List<TimeEntry> TimeEntries { get; set; } = new();
    public List<SelectListItem> StatusList { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public double TotalHours { get; set; }
    public decimal TotalWage { get; set; }
    public int PendingCount { get; set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        // Standard periode: denne måned
        if (!FromDate.HasValue)
            FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (!ToDate.HasValue)
            ToDate = DateTime.Today.AddDays(1).AddTicks(-1);

        // Status dropdown
        StatusList = new List<SelectListItem>
        {
            new SelectListItem { Value = "Pending", Text = "Afventer" },
            new SelectListItem { Value = "Approved", Text = "Godkendt" },
            new SelectListItem { Value = "Rejected", Text = "Afvist" }
        };

        // Hent time entries
        var query = _context.TimeEntries
            .Include(t => t.Employee)
            .Include(t => t.Department)
            .Where(t => t.TenantId == user.TenantId
                && t.ClockIn >= FromDate
                && t.ClockIn <= ToDate);

        if (!string.IsNullOrEmpty(Status))
        {
            query = query.Where(t => t.Status == Status);
        }

        TimeEntries = await query
            .OrderByDescending(t => t.ClockIn)
            .ToListAsync();

        // Beregn totals
        var completedEntries = TimeEntries.Where(t => t.ClockOut.HasValue).ToList();
        TotalHours = completedEntries.Sum(t =>
            (t.ClockOut.Value - t.ClockIn - (t.BreakDuration ?? TimeSpan.Zero)).TotalHours);
        TotalWage = completedEntries.Sum(t => t.CalculatedWage ?? 0);
        PendingCount = TimeEntries.Count(t => t.Status == "Pending" && t.ClockOut.HasValue);
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        var entry = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == user.TenantId);

        if (entry != null && entry.ClockOut.HasValue)
        {
            entry.Status = "Approved";
            entry.ApprovedById = Guid.Parse(userId);
            entry.ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        var entry = await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == user.TenantId);

        if (entry != null)
        {
            entry.Status = "Rejected";
            entry.ApprovedById = Guid.Parse(userId);
            entry.ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}