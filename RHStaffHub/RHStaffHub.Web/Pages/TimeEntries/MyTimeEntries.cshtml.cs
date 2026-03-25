// Pages/TimeEntries/MyTimeEntries.cshtml.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;  // <-- ÆNDRET FRA Infrastructure.Data til Web.Data

namespace RHStaffHub.Web.Pages.TimeEntries
{
    [Authorize(Roles = "Employee,Manager,Admin")]
    public class MyTimeEntriesModel : PageModel
    {
        private readonly ApplicationDbContext _context;  // <-- ÆNDRET FRA AppDbContext
        private readonly UserManager<ApplicationUser> _userManager;

        public MyTimeEntriesModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)  // <-- ÆNDRET
        {
            _context = context;
            _userManager = userManager;
        }

        // Properties til View
        public List<TimeEntry> TimeEntries { get; set; } = new();
        public TimeEntry? ActiveEntry { get; set; }
        public SelectList Departments { get; set; } = default!;

        // Statistik
        public double MonthlyHours { get; set; }
        public decimal MonthlyWage { get; set; }
        public int ApprovedCount { get; set; }

        // GPS Fejlmeddelelse
        [TempData]
        public string GpsError { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await LoadDataAsync(user);
            return Page();
        }

        private async Task LoadDataAsync(ApplicationUser user)
        {
            var userTenantId = user.TenantId;

            // Hent alle brugerens time entries (nyeste først)
            TimeEntries = await _context.TimeEntries
                .Include(t => t.Department)
                .Where(t => t.EmployeeId == user.Id && t.TenantId == userTenantId)
                .OrderByDescending(t => t.ClockIn)
                .Take(50)
                .ToListAsync();

            // Find aktiv entry (ikke clocket ud endnu)
            ActiveEntry = await _context.TimeEntries
                .Include(t => t.Department)
                .FirstOrDefaultAsync(t => t.EmployeeId == user.Id && t.ClockOut == null);

            // Hent afdelinger til dropdown (kun brugerens egne afdelinger)
            var departments = await _context.Departments
                .Where(d => d.IsActive && d.TenantId == userTenantId)
                .OrderBy(d => d.Name)
                .ToListAsync();
            Departments = new SelectList(departments, "Id", "Name");

            // Beregn månedsstatistik
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var monthlyEntries = TimeEntries
                .Where(t => t.ClockIn >= startOfMonth && t.ClockOut.HasValue)
                .ToList();

            MonthlyHours = monthlyEntries.Sum(t =>
                (t.ClockOut!.Value - t.ClockIn - (t.BreakDuration ?? TimeSpan.Zero)).TotalHours);

            MonthlyWage = 0;
            ApprovedCount = monthlyEntries.Count(t => t.Status == "Approved");
        }

        public async Task<IActionResult> OnPostClockInAsync(
            Guid departmentId,
            double? latitude,
            double? longitude)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var userTenantId = user.TenantId;

            // Hent afdeling med GPS info
            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.TenantId == userTenantId);

            if (department == null)
            {
                GpsError = "Afdeling ikke fundet.";
                await LoadDataAsync(user);
                return Page();
            }

            // Tjek om bruger allerede er clocket ind
            var existingActive = await _context.TimeEntries
                .FirstOrDefaultAsync(t => t.EmployeeId == user.Id && t.ClockOut == null);

            if (existingActive != null)
            {
                GpsError = "Du er allerede clocket ind. Clock ud først.";
                await LoadDataAsync(user);
                return Page();
            }

            // GPS VALIDERING
            if (department.GpsLatitude.HasValue && department.GpsLongitude.HasValue)
            {
                if (!latitude.HasValue || !longitude.HasValue)
                {
                    GpsError = "GPS position påkrævet. Tillad adgang til din placering.";
                    await LoadDataAsync(user);
                    return Page();
                }

                // Beregn afstand (Haversine formula)
                var distance = CalculateDistance(
                    latitude.Value, longitude.Value,
                    department.GpsLatitude.Value, department.GpsLongitude.Value);

                var allowedRadius = department.GpsRadiusMeters;

                if (distance > allowedRadius)
                {
                    GpsError = $"Du er {distance:F0} meter fra afdelingen. " +
                              $"Du skal være inden for {allowedRadius} meter for at checke ind.";
                    await LoadDataAsync(user);
                    return Page();
                }
            }

            // Opret ny time entry
            var entry = new TimeEntry
            {
                Id = Guid.NewGuid(),
                EmployeeId = user.Id,
                DepartmentId = departmentId,
                ClockIn = DateTime.Now,
                ClockInLatitude = latitude,
                ClockInLongitude = longitude,
                Status = "Pending",
                TenantId = userTenantId
            };

            _context.TimeEntries.Add(entry);
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostClockOutAsync(
            double? latitude,
            double? longitude)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Find aktiv entry
            var entry = await _context.TimeEntries
                .FirstOrDefaultAsync(t => t.EmployeeId == user.Id && t.ClockOut == null);

            if (entry == null)
            {
                GpsError = "Ingen aktiv clock-in fundet.";
                await LoadDataAsync(user);
                return Page();
            }

            // Clock ud
            entry.ClockOut = DateTime.Now;
            entry.ClockOutLatitude = latitude;
            entry.ClockOutLongitude = longitude;

            // Beregn timer og løn
            var workedHours = (entry.ClockOut.Value - entry.ClockIn - (entry.BreakDuration ?? TimeSpan.Zero)).TotalHours;
            entry.CalculatedWage = (decimal)workedHours * user.HourlyRate;

            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        // Beregn afstand mellem to GPS koordinater (Haversine formula)
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Jordens radius i meter

            var lat1Rad = ToRadians(lat1);
            var lat2Rad = ToRadians(lat2);
            var deltaLat = ToRadians(lat2 - lat1);
            var deltaLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c; // Afstand i meter
        }

        private double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }
    }
}