using Microsoft.EntityFrameworkCore;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext context)
    : BaseRepository<User, Guid, IdentityDbContext>(context), IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default) =>
        await DbSet
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant() && u.TenantId == tenantId, ct);

    public async Task<User?> GetByEmailWithRolesAsync(string email, CancellationToken ct = default) =>
        await DbSet
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<User?> GetWithRefreshTokensAsync(Guid id, CancellationToken ct = default) =>
        await DbSet
            .Include(u => u.RefreshTokens)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
}
