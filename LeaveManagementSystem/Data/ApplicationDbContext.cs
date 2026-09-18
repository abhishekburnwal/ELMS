using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(150).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            // NOTE: no HasDefaultValue for LeaveBalance — the entity initializer
            // (= 20) is the default for new users, and omitting a DB default lets
            // the seed insert the Admin's explicit 0 (DATABASE.md §4).
            entity.Property(u => u.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.Property(l => l.FromDate).HasColumnType("date");
            entity.Property(l => l.ToDate).HasColumnType("date");
            entity.Property(l => l.Reason).HasMaxLength(500).IsRequired();
            entity.Property(l => l.Status).HasDefaultValue(LeaveStatus.Pending);
            entity.Property(l => l.AppliedDate).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(l => l.Remarks).HasMaxLength(500);

            entity.HasOne(l => l.User)
                .WithMany(u => u.LeaveRequests)
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.ReviewedBy)
                .WithMany(u => u.ReviewedRequests)
                .HasForeignKey(l => l.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
                t.HasCheckConstraint("CK_LeaveRequests_DateRange", "[ToDate] >= [FromDate]"));

            entity.HasIndex(l => new { l.UserId, l.FromDate, l.ToDate })
                .HasDatabaseName("IX_LeaveRequests_UserId_Dates");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(a => a.Action).HasMaxLength(50).IsRequired();
            entity.Property(a => a.ActionDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(a => a.LeaveRequest)
                .WithMany()
                .HasForeignKey(a => a.LeaveRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.ActionBy)
                .WithMany()
                .HasForeignKey(a => a.ActionByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed accounts per DATABASE.md §4 / PROJECT.md §5.
        // Hashes are generated here at model-building time and baked into the
        // InitialCreate migration as literals — never stored as plaintext.
        var hasher = new PasswordHasher<User>();
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                FullName = "System Admin",
                Email = "admin@example.com",
                PasswordHash = hasher.HashPassword(null!, "admin123"),
                Role = UserRole.Admin,
                IsActive = true,
                LeaveBalance = 0,
                CreatedDate = seedDate
            },
            new User
            {
                Id = 2,
                FullName = "Demo Employee",
                Email = "employee@example.com",
                PasswordHash = hasher.HashPassword(null!, "emp123"),
                Role = UserRole.Employee,
                IsActive = true,
                LeaveBalance = 20,
                CreatedDate = seedDate
            });
    }
}
