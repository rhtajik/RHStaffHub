using System.ComponentModel.DataAnnotations.Schema;

namespace RHStaffHub.Domain.Entities;

public class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign keys
    public Guid EmployeeId { get; set; }

    [ForeignKey("EmployeeId")]
    public virtual ApplicationUser Employee { get; set; } = null!;

    public Guid DepartmentId { get; set; }

    [ForeignKey("DepartmentId")]
    public virtual Department Department { get; set; } = null!;

    public DateTime ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public TimeSpan? BreakDuration { get; set; }

    public double? ClockInLatitude { get; set; }
    public double? ClockInLongitude { get; set; }
    public double? ClockOutLatitude { get; set; }
    public double? ClockOutLongitude { get; set; }

    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public decimal? CalculatedWage { get; set; }
    public decimal? OvertimeHours { get; set; }

    public string TenantId { get; set; } = string.Empty;
}