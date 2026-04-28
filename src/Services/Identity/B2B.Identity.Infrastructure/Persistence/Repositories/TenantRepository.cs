using Microsoft.EntityFrameworkCore;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Infrastructure.Persistence;

namespace B2B.Identity.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(IdentityDbContext context)
    : BaseRepository<Tenant, Guid, IdentityDbContext>(context), ITenantRepository
{
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await DbSet.FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant(), ct);

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        await DbSet.AnyAsync(t => t.Slug == slug.ToLowerInvariant(), ct);
}
