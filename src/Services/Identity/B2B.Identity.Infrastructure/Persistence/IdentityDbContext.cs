using Microsoft.EntityFrameworkCore;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : BaseDbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).IsRequired().HasMaxLength(FieldLengths.Name);
            b.Property(e => e.Slug).IsRequired().HasMaxLength(FieldLengths.Slug);
            b.HasIndex(e => e.Slug).IsUnique();
            b.Property(e => e.Domain).HasMaxLength(FieldLengths.Domain);
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Email).IsRequired().HasMaxLength(FieldLengths.Email);
            b.HasIndex(e => new { e.Email, e.TenantId }).IsUnique();
            b.Property(e => e.FirstName).IsRequired().HasMaxLength(FieldLengths.ShortName);
            b.Property(e => e.LastName).IsRequired().HasMaxLength(FieldLengths.ShortName);
            b.Property(e => e.PasswordHash).IsRequired().HasMaxLength(FieldLengths.PasswordHash);
            b.HasOne(e => e.Tenant).WithMany(t => t.Users).HasForeignKey(e => e.TenantId);
            b.HasMany(e => e.RefreshTokens).WithOne().HasForeignKey(r => r.UserId);
        });

        modelBuilder.Entity<Role>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Name).IsRequired().HasMaxLength(FieldLengths.ShortName);
            b.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<UserRole>(b =>
        {
            b.HasKey(e => new { e.UserId, e.RoleId });
            b.HasOne(e => e.User).WithMany(u => u.UserRoles).HasForeignKey(e => e.UserId);
            b.HasOne(e => e.Role).WithMany(r => r.UserRoles).HasForeignKey(e => e.RoleId);
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Token).IsRequired().HasMaxLength(FieldLengths.Token);
            b.HasIndex(e => e.Token);
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var superAdminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantAdminRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var userRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var readOnlyRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        modelBuilder.Entity<Role>().HasData(
            new { Id = superAdminRoleId, Name = "SuperAdmin", Description = "Super Administrator", IsSystem = true, CreatedAt = DateTime.UtcNow },
            new { Id = tenantAdminRoleId, Name = "TenantAdmin", Description = "Tenant Administrator", IsSystem = true, CreatedAt = DateTime.UtcNow },
            new { Id = userRoleId, Name = "User", Description = "Regular User", IsSystem = true, CreatedAt = DateTime.UtcNow },
            new { Id = readOnlyRoleId, Name = "ReadOnly", Description = "Read Only User", IsSystem = true, CreatedAt = DateTime.UtcNow }
        );
    }
}
