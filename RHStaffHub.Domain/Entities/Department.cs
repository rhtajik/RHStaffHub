using System.ComponentModel.DataAnnotations.Schema;

namespace RHStaffHub.Domain.Entities;

public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? LocationId { get; set; }

    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public int GpsRadiusMeters { get; set; } = 100;

    public bool IsActive { get; set; } = true;  // <-- TILFØJ DENNE LINJE

    public string TenantId { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }

    [ForeignKey("CompanyId")]
    public virtual Company Company { get; set; } = null!;

    [NotMapped]
    public virtual ICollection<ApplicationUser> Employees { get; set; } = new List<ApplicationUser>();

    [NotMapped]
    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    [NotMapped]
    public virtual ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}