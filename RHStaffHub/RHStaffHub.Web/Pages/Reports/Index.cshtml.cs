using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;
using RHStaffHub.Web.Data;
using System.Security.Claims;
using System.Text;

namespace RHStaffHub.Web.Pages.Reports;

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

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> Employees { get; set; } = new();
    public List<ReportItem> ReportData { get; set; } = new();

    [BindProperty]
    public DateTime? FromDate { get; set; }

    [BindProperty]
    public DateTime? ToDate { get; set; }

    [BindProperty]
    public Guid? DepartmentId { get; set; }

    [BindProperty]
    public Guid? EmployeeId { get; set; }

    public double TotalHours { get; set; }
    public decimal TotalWage { get; set; }
    public decimal AverageHourlyRate { get; set; }
    public int TotalShifts { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDate = DateTime.Today;

        await LoadDropdowns(user.TenantId);
        await LoadReportData(user.TenantId);

        return Page();
    }

    public async Task<IActionResult> OnPostViewAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        await LoadDropdowns(user.TenantId);
        await LoadReportData(user.TenantId);

        return Page();
    }

    public async Task<IActionResult> OnPostExportAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        if (!FromDate.HasValue)
            FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (!ToDate.HasValue)
            ToDate = DateTime.Today;

        await LoadDropdowns(user.TenantId);
        await LoadReportData(user.TenantId);

        // Generer CSV med semikolon separator (dansk format)
        var csv = new StringBuilder();

        // Header med BOM for Excel
        csv.AppendLine("Medarbejder;Afdeling;Timer;Løn;Gns Timeløn;Godkendte;Afventer");

        foreach (var item in ReportData)
        {
            var line = $"{EscapeCsv(item.EmployeeName)};" +
                       $"{EscapeCsv(item.DepartmentName)};" +
                       $"{item.TotalHours.ToString("F2").Replace(",", ".")};" +
                       $"{item.TotalWage.ToString("F2").Replace(",", ".")};" +
                       $"{item.AverageHourlyRate.ToString("F2").Replace(",", ".")};" +
                       $"{item.ApprovedCount};" +
                       $"{item.PendingCount}";

            csv.AppendLine(line);
        }

        // Tilføj BOM (Byte Order Mark) så Excel genkender UTF-8
        var preamble = Encoding.UTF8.GetPreamble();
        var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
        var result = new byte[preamble.Length + csvBytes.Length];

        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(csvBytes, 0, result, preamble.Length, csvBytes.Length);

        return File(result, "text/csv; charset=utf-8", $"rapport_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> OnPostExportExcelAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        if (!FromDate.HasValue)
            FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (!ToDate.HasValue)
            ToDate = DateTime.Today;

        await LoadDropdowns(user.TenantId);
        await LoadReportData(user.TenantId);

        // Generer HTML-tabel som Excel kan åbne
        var html = new StringBuilder();
        html.AppendLine("<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:x='urn:schemas-microsoft-com:office:excel' xmlns='http://www.w3.org/TR/REC-html40'>");
        html.AppendLine("<head><meta charset='UTF-8'><style>td, th { border: 1px solid #ddd; padding: 8px; }</style></head>");
        html.AppendLine("<body>");
        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Medarbejder</th><th>Afdeling</th><th>Timer</th><th>Løn</th><th>Gns Timeløn</th><th>Godkendte</th><th>Afventer</th></tr>");

        foreach (var item in ReportData)
        {
            html.AppendLine("<tr>");
            html.AppendLine($"<td>{item.EmployeeName}</td>");
            html.AppendLine($"<td>{item.DepartmentName}</td>");
            html.AppendLine($"<td>{item.TotalHours:F2}</td>");
            html.AppendLine($"<td>{item.TotalWage:F2}</td>");
            html.AppendLine($"<td>{item.AverageHourlyRate:F2}</td>");
            html.AppendLine($"<td>{item.ApprovedCount}</td>");
            html.AppendLine($"<td>{item.PendingCount}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
        html.AppendLine("</body></html>");

        var bytes = Encoding.UTF8.GetBytes(html.ToString());
        return File(bytes, "application/vnd.ms-excel", $"rapport_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.xls");
    }

    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        if (field.Contains(";") || field.Contains("\"") || field.Contains("\n"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }

    private async Task LoadDropdowns(string tenantId)
    {
        Departments = await _context.Departments
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
            .ToListAsync();

        Employees = await _context.Users
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Role != "Admin")
            .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.FullName })
            .ToListAsync();
    }

    private async Task LoadReportData(string tenantId)
    {
        var query = _context.TimeEntries
            .Include(t => t.Employee)
            .Include(t => t.Department)
            .Where(t => t.TenantId == tenantId
                && t.ClockIn >= FromDate
                && t.ClockIn <= ToDate.Value.AddDays(1)
                && t.ClockOut.HasValue);

        if (DepartmentId.HasValue)
            query = query.Where(t => t.DepartmentId == DepartmentId.Value);

        if (EmployeeId.HasValue)
            query = query.Where(t => t.EmployeeId == EmployeeId.Value);

        var entries = await query.ToListAsync();

        ReportData = entries
            .GroupBy(t => new { t.EmployeeId, t.Employee!.FullName, t.DepartmentId, t.Department!.Name })
            .Select(g => new ReportItem
            {
                EmployeeName = g.Key.FullName,
                DepartmentName = g.Key.Name,
                TotalHours = g.Sum(t => (t.ClockOut!.Value - t.ClockIn - (t.BreakDuration ?? TimeSpan.Zero)).TotalHours),
                TotalWage = g.Sum(t => t.CalculatedWage ?? 0),
                AverageHourlyRate = g.Average(t => t.CalculatedWage.HasValue
                    ? t.CalculatedWage.Value / (decimal)(t.ClockOut!.Value - t.ClockIn - (t.BreakDuration ?? TimeSpan.Zero)).TotalHours
                    : 0),
                ApprovedCount = g.Count(t => t.Status == "Approved"),
                PendingCount = g.Count(t => t.Status == "Pending")
            })
            .OrderByDescending(r => r.TotalHours)
            .ToList();

        TotalHours = ReportData.Sum(r => r.TotalHours);
        TotalWage = ReportData.Sum(r => r.TotalWage);
        AverageHourlyRate = TotalHours > 0 ? TotalWage / (decimal)TotalHours : 0;
        TotalShifts = entries.Count;
    }

    public class ReportItem
    {
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public double TotalHours { get; set; }
        public decimal TotalWage { get; set; }
        public decimal AverageHourlyRate { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingCount { get; set; }
    }
}