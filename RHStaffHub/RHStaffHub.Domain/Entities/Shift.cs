using System.ComponentModel.DataAnnotations.Schema;

namespace RHStaffHub.Domain.Entities;

public class Shift
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign keys
    public Guid DepartmentId { get; set; }

    [ForeignKey("DepartmentId")]
    public virtual Department Department { get; set; } = null!;

    public Guid? EmployeeId { get; set; }

    [ForeignKey("EmployeeId")]
    public virtual ApplicationUser? Employee { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public string? Title { get; set; }
    public string? Notes { get; set; }

    public string Status { get; set; } = "Scheduled";

    public bool IsSwapRequested { get; set; } = false;
    public Guid? SwapRequestedToId { get; set; }

    public bool IsApproved { get; set; } = false;
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public bool IsRecurring { get; set; } = false;
    public string? RecurrencePattern { get; set; }

    public string TenantId { get; set; } = string.Empty;
    public TimeSpan Duration => EndTime - StartTime;
}