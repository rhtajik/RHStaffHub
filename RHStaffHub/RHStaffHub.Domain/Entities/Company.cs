using System.ComponentModel.DataAnnotations.Schema;

namespace RHStaffHub.Domain.Entities;

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? CVR { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public decimal StandardHourlyRate { get; set; } = 150.00m;
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    public int WeeklyHoursThreshold { get; set; } = 37;

    // Navigation - IGNORER for nu
    [NotMapped]
    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    [NotMapped]
    public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}