using B2B.Identity.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Application.Commands.ChangePassword;

public sealed class ChangePasswordHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, cancellationToken);
        if (user is null)
            return Error.NotFound("User.NotFound", "User not found.");

        var passwordValid = await passwordHasher.VerifyAsync(request.CurrentPassword, user.PasswordHash, cancellationToken);
        if (!passwordValid)
            return Error.Validation("User.InvalidPassword", "Current password is incorrect.");

        var newHash = await passwordHasher.HashAsync(request.NewPassword, cancellationToken);

        // Using reflection to update PasswordHash since it is private-set — done via the
        // domain method to preserve encapsulation.
        // Domain entities expose a dedicated mutator; calling it here is the correct approach.
        user.UpdatePassword(newHash);
        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
