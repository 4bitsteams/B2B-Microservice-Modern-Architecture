namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Async interface — BCrypt is CPU-bound; implementations must use Task.Run
/// so the thread pool is not blocked under high login/register concurrency.
/// </summary>
public interface IPasswordHasher
{
    Task<string> HashAsync(string password, CancellationToken ct = default);
    Task<bool> VerifyAsync(string password, string hash, CancellationToken ct = default);
}
