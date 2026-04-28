using BCrypt.Net;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Security;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    // BCrypt is deliberately CPU-intensive (work factor ~12). Running it on a
    // thread-pool thread via Task.Run prevents it from starving async I/O threads
    // under high concurrent login / register load.
    public Task<string> HashAsync(string password, CancellationToken ct = default) =>
        Task.Run(() => BCrypt.Net.BCrypt.HashPassword(password), ct);

    public Task<bool> VerifyAsync(string password, string hash, CancellationToken ct = default) =>
        Task.Run(() => BCrypt.Net.BCrypt.Verify(password, hash), ct);
}
