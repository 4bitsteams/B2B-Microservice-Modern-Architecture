using B2B.Identity.Domain.Entities;
using B2B.Identity.Application.Interfaces;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.CQRS;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Application.Commands.RegisterUser;

public sealed class RegisterUserHandler(
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    IRoleRepository roleRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RegisterUserCommand, RegisterUserResponse>
{
    public async Task<Result<RegisterUserResponse>> Handle(
        RegisterUserCommand request, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, ct);
        if (tenant is null)
            return Error.NotFound("Tenant.NotFound", $"Tenant '{request.TenantSlug}' not found.");

        var exists = await userRepository.ExistsAsync(
            u => u.Email == request.Email.ToLowerInvariant() && u.TenantId == tenant.Id, ct);
        if (exists)
            return Error.Conflict("User.AlreadyExists", "A user with this email already exists.");

        var passwordHash = await passwordHasher.HashAsync(request.Password, ct);
        var user = User.Create(request.FirstName, request.LastName, request.Email, passwordHash, tenant.Id);

        var userRole = await roleRepository.GetByNameAsync(Role.SystemRoles.User, ct);
        if (userRole is not null)
            user.AssignRole(userRole.Id);

        await userRepository.AddAsync(user, ct);

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            // Concurrent registration with the same email arrived between ExistsAsync and SaveChanges.
            return Error.Conflict("User.AlreadyExists", "A user with this email already exists.");
        }

        return new RegisterUserResponse(user.Id, user.Email, user.FullName);
    }
}
