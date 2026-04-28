using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using B2B.Identity.Application.Commands.RegisterUser;
using B2B.Identity.Application.Interfaces;
using B2B.Identity.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;

namespace B2B.Identity.Tests.Application;

public sealed class RegisterUserHandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly ITenantRepository _tenantRepo = Substitute.For<ITenantRepository>();
    private readonly IRoleRepository _roleRepo = Substitute.For<IRoleRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly RegisterUserHandler _handler;

    private static readonly Tenant TestTenant = Tenant.Create("Acme Corp", "acme");
    private static readonly Role DefaultRole = Role.Create(Role.SystemRoles.User);

    private static readonly RegisterUserCommand ValidCommand = new(
        "Jane", "Doe", "jane@acme.com", "P@ssw0rd!", "acme");

    public RegisterUserHandlerTests()
    {
        _handler = new RegisterUserHandler(
            _userRepo, _tenantRepo, _roleRepo, _hasher, _unitOfWork);

        // Default happy-path stubs
        _tenantRepo.GetBySlugAsync("acme", default).Returns(TestTenant);
        _userRepo.ExistsAsync(default!, default).ReturnsForAnyArgs(false);
        _hasher.HashAsync(Arg.Any<string>(), default).Returns("hashed_pw");
        _roleRepo.GetByNameAsync(Role.SystemRoles.User, default).Returns(DefaultRole);
        _unitOfWork.SaveChangesAsync(default).Returns(1);
    }

    // ──────────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidData_ShouldReturnUserResponse()
    {
        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("jane@acme.com");
        result.Value.FullName.Should().Be("Jane Doe");
        result.Value.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldHashPassword()
    {
        await _handler.Handle(ValidCommand, default);

        await _hasher.Received(1).HashAsync("P@ssw0rd!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPersistUser()
    {
        await _handler.Handle(ValidCommand, default);

        await _userRepo.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "jane@acme.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSaveChanges()
    {
        await _handler.Handle(ValidCommand, default);

        await _unitOfWork.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ShouldAssignDefaultUserRole()
    {
        User? capturedUser = null;
        await _userRepo.AddAsync(
            Arg.Do<User>(u => capturedUser = u),
            Arg.Any<CancellationToken>());

        await _handler.Handle(ValidCommand, default);

        capturedUser.Should().NotBeNull();
        capturedUser!.UserRoles.Should().ContainSingle(ur => ur.RoleId == DefaultRole.Id);
    }

    [Fact]
    public async Task Handle_WhenDefaultRoleNotFound_ShouldStillCreateUser()
    {
        _roleRepo.GetByNameAsync(Role.SystemRoles.User, default).Returns((Role?)null);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeTrue();
    }

    // ──────────────────────────────────────────────
    // Tenant not found
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithNonExistentTenant_ShouldReturnNotFound()
    {
        _tenantRepo.GetBySlugAsync("acme", default).Returns((Tenant?)null);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tenant.NotFound");
    }

    // ──────────────────────────────────────────────
    // Duplicate email (pre-check)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldReturnConflict()
    {
        _userRepo.ExistsAsync(default!, default).ReturnsForAnyArgs(true);

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.AlreadyExists");
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldNotCallSaveChanges()
    {
        _userRepo.ExistsAsync(default!, default).ReturnsForAnyArgs(true);

        await _handler.Handle(ValidCommand, default);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(default);
    }

    // ──────────────────────────────────────────────
    // Concurrent duplicate (race condition → UniqueConstraintException)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenConcurrentDuplicateRacesToDb_ShouldReturnConflict()
    {
        // ExistsAsync passes (no duplicate seen yet), but DB raises unique violation
        _unitOfWork.SaveChangesAsync(default)
            .ThrowsAsync(new UniqueConstraintException("duplicate key"));

        var result = await _handler.Handle(ValidCommand, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.AlreadyExists");
    }
}
