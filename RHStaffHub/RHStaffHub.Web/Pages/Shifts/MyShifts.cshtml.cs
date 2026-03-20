using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Globalization;
using System.Security.Claims;

namespace RHStaffHub.Web.Pages.Shifts;

[Authorize] // Alle roller har adgang
public class MyShiftsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MyShiftsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public List<Shift> Shifts { get; set; } = new();
    public string CurrentWeek { get; set; } = string.Empty;
    public string PreviousWeek { get; set; } = string.Empty;
    public string NextWeek { get; set; } = string.Empty;

    public async Task OnGetAsync(string? week)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        // Beregn ugenumre korrekt
        var today = DateTime.Today;
        var currentYear = today.Year;
        var currentWeekNumber = ISOWeek.GetWeekOfYear(today);

        if (string.IsNullOrEmpty(week))
        {
            CurrentWeek = $"{currentYear}-W{currentWeekNumber:D2}";
        }
        else
        {
            CurrentWeek = week;
        }

        // Parse ugen
        var parts = CurrentWeek.Split("-W");
        if (parts.Length == 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var weekNum))
        {
            // Beregn forrige uge
            if (weekNum > 1)
            {
                PreviousWeek = $"{year}-W{weekNum - 1:D2}";
            }
            else
            {
                // Gå til sidste uge i foregående år
                var lastWeekPrevYear = ISOWeek.GetWeeksInYear(year - 1);
                PreviousWeek = $"{year - 1}-W{lastWeekPrevYear:D2}";
            }

            // Beregn næste uge
            var weeksInYear = ISOWeek.GetWeeksInYear(year);
            if (weekNum < weeksInYear)
            {
                NextWeek = $"{year}-W{weekNum + 1:D2}";
            }
            else
            {
                // Gå til første uge i næste år
                NextWeek = $"{year + 1}-W01";
            }

            // Hent vagter for denne uge
            var startDate = ISOWeek.ToDateTime(year, weekNum, DayOfWeek.Monday);
            var endDate = startDate.AddDays(6).AddHours(23).AddMinutes(59);

            Shifts = await _context.Shifts
                .Include(s => s.Department)
                .Where(s => s.EmployeeId == user.Id
                    && s.StartTime >= startDate
                    && s.StartTime <= endDate
                    && s.Status != "Cancelled")
                .OrderBy(s => s.StartTime)
                .ToListAsync();
        }
    }
}