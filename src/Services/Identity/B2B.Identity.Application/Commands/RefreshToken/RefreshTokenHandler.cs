using B2B.Identity.Application.Commands.Login;
using B2B.Identity.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Application.Commands.RefreshToken;

public sealed class RefreshTokenHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var (userId, _) = tokenService.ValidateToken(request.AccessToken);

        var user = await userRepository.GetWithRefreshTokensAsync(userId, ct);
        if (user is null)
            return Error.Unauthorized("Auth.InvalidToken", "Invalid token.");

        var token = user.RefreshTokens.FirstOrDefault(t => t.Token == request.RefreshToken);
        if (token is null || !token.IsActive)
            return Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token is invalid or expired.");

        var roles = await roleRepository.GetByUserIdAsync(user.Id, ct);
        var roleNames = roles.Select(r => r.Name).ToList();

        var newRefreshToken = tokenService.GenerateRefreshToken();
        var accessToken = tokenService.GenerateAccessToken(user, roleNames);
        var expiry = DateTime.UtcNow.AddHours(1);

        user.RevokeRefreshToken(request.RefreshToken);
        user.AddRefreshToken(newRefreshToken, expiry.AddDays(7));
        await unitOfWork.SaveChangesAsync(ct);

        return new LoginResponse(accessToken, newRefreshToken, expiry, user.Id, user.FullName, roleNames);
    }
}
