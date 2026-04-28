using B2B.Identity.Application.Commands.Login;
using B2B.Shared.Core.CQRS;

namespace B2B.Identity.Application.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string AccessToken, string RefreshToken) : ICommand<LoginResponse>;
