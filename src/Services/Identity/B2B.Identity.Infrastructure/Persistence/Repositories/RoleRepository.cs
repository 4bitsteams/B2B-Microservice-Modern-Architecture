using Microsoft.EntityFrameworkCore;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Identity.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository(IdentityDbContext context)
    : BaseRepository<Role, Guid, IdentityDbContext>(context), IRoleRepository
{
    public async Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(r => r.Name == name, ct);

    public async Task<IReadOnlyList<Role>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await Context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role)
            .ToListAsync(ct);
}
