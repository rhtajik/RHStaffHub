using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RHStaffHub.Domain.Entities;

namespace RHStaffHub.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    public DbSet<Shift> Shifts { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Indeks for performance
        builder.Entity<TimeEntry>()
            .HasIndex(t => new { t.TenantId, t.EmployeeId, t.ClockIn });

        builder.Entity<Shift>()
            .HasIndex(s => new { s.TenantId, s.DepartmentId, s.StartTime });

        builder.Entity<Department>()
            .HasIndex(d => new { d.TenantId, d.CompanyId });

        builder.Entity<ApplicationUser>()
            .HasIndex(u => new { u.TenantId, u.Email });

        // Soft delete filter
        builder.Entity<ApplicationUser>()
            .HasQueryFilter(u => u.IsActive);
    }
}