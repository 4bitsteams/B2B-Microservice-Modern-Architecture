using B2B.Discount.Application.Commands.CreateDiscount;
using B2B.Discount.Application.Interfaces;
using B2B.Discount.Domain.Entities;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using DiscountEntity = B2B.Discount.Domain.Entities.Discount;

namespace B2B.Discount.Tests.Application;

public sealed class CreateDiscountHandlerTests
{
    private readonly IDiscountRepository _repo = Substitute.For<IDiscountRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly CreateDiscountHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public CreateDiscountHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new CreateDiscountHandler(_repo, _currentUser, _uow);
    }

    [Fact]
    public async Task Handle_Valid_ShouldReturnSuccess()
    {
        var cmd = new CreateDiscountCommand("Spring Sale", DiscountType.Percentage, 20m);

        var result = await _handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Spring Sale");
        result.Value.Type.Should().Be("Percentage");
        result.Value.Value.Should().Be(20m);
    }

    [Fact]
    public async Task Handle_Valid_ShouldPersist()
    {
        var cmd = new CreateDiscountCommand("Spring Sale", DiscountType.Percentage, 20m);

        await _handler.Handle(cmd, default);

        await _repo.Received(1).AddAsync(Arg.Any<DiscountEntity>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStampCurrentTenantId()
    {
        DiscountEntity? captured = null;
        await _repo.AddAsync(Arg.Do<DiscountEntity>(d => captured = d), Arg.Any<CancellationToken>());
        var cmd = new CreateDiscountCommand("Spring Sale", DiscountType.Percentage, 20m);

        await _handler.Handle(cmd, default);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
    }
}
