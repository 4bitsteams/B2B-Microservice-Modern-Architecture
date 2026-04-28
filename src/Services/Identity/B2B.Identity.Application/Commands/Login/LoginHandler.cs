using B2B.Identity.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Application.Commands.Login;

public sealed class LoginHandler(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    IRoleRepository roleRepository,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, ct);
        if (tenant is null)
            return Error.NotFound("Tenant.NotFound", $"Tenant '{request.TenantSlug}' not found.");

        var user = await userRepository.GetByEmailAsync(request.Email, tenant.Id, ct);
        if (user is null)
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

        if (user.IsLocked)
            return Error.Unauthorized("Auth.AccountLocked", "Account is temporarily locked. Try again later.");

        if (!await passwordHasher.VerifyAsync(request.Password, user.PasswordHash, ct))
        {
            user.RecordFailedLogin();
            await unitOfWork.SaveChangesAsync(ct);
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
        }

        var roles = await roleRepository.GetByUserIdAsync(user.Id, ct);
        var roleNames = roles.Select(r => r.Name).ToList();

        var accessToken = tokenService.GenerateAccessToken(user, roleNames);
        var refreshToken = tokenService.GenerateRefreshToken();
        var expiry = DateTime.UtcNow.AddHours(1);

        user.RecordLogin();
        user.AddRefreshToken(refreshToken, expiry.AddDays(7));
        await unitOfWork.SaveChangesAsync(ct);

        return new LoginResponse(accessToken, refreshToken, expiry, user.Id, user.FullName, roleNames);
    }
}
