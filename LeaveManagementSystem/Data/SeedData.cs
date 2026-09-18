using LeaveManagementSystem.Models.Entities;
using LeaveManagementSystem.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagementSystem.Data;

// Runtime idempotency guard for the two default accounts (DATABASE.md §4).
// Migrations (HasData) insert them on `dotnet ef database update`; this ensures
// they also exist if the database was created any other way. Never duplicates.
public static class SeedData
{
    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var hasher = new PasswordHasher<User>();
        EnsureUser(db, hasher, 1, "System Admin", "admin@example.com", "admin123", UserRole.Admin, 0);
        EnsureUser(db, hasher, 2, "Demo Employee", "employee@example.com", "emp123", UserRole.Employee, 20);

        db.SaveChanges();
    }

    private static void EnsureUser(
        ApplicationDbContext db,
        PasswordHasher<User> hasher,
        int id,
        string fullName,
        string email,
        string plainPassword,
        UserRole role,
        int leaveBalance)
    {
        if (db.Users.Any(u => u.Email == email))
        {
            return;
        }

        db.Users.Add(new User
        {
            Id = id,
            FullName = fullName,
            Email = email,
            PasswordHash = hasher.HashPassword(null!, plainPassword),
            Role = role,
            IsActive = true,
            LeaveBalance = leaveBalance,
            CreatedDate = DateTime.UtcNow
        });
    }
}
