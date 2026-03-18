using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace RHStaffHub.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? EmployeeNumber { get; set; }

    public string Role { get; set; } = "Employee";

    public decimal HourlyRate { get; set; }
    public DateTime? EmploymentStartDate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string TenantId { get; set; } = string.Empty;

    // Foreign key til Department
    public Guid? PrimaryDepartmentId { get; set; }

    [ForeignKey("PrimaryDepartmentId")]
    public virtual Department? PrimaryDepartment { get; set; }

    // Navigation - IGNORER disse i DbContext for nu
    [NotMapped]
    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    [NotMapped]
    public virtual ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
}