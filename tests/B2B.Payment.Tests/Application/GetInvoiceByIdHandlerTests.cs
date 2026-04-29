using B2B.Payment.Application.Interfaces;
using B2B.Payment.Application.Queries.GetInvoiceById;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Payment.Tests.Application;

public sealed class GetInvoiceByIdHandlerTests
{
    private readonly IReadInvoiceRepository _repo = Substitute.For<IReadInvoiceRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetInvoiceByIdHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetInvoiceByIdHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetInvoiceByIdHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnNotFound()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Invoice?)null);

        var result = await _handler.Handle(new GetInvoiceByIdQuery(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnNotFound()
    {
        var inv = Invoice.Create("X", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, 0m, "USD");
        _repo.GetByIdAsync(inv.Id, Arg.Any<CancellationToken>()).Returns(inv);

        var result = await _handler.Handle(new GetInvoiceByIdQuery(inv.Id), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Found_ShouldReturnDto()
    {
        var inv = Invoice.Create("INV-9", Guid.NewGuid(), Guid.NewGuid(), TenantId, 100m, 5m, "USD");
        _repo.GetByIdAsync(inv.Id, Arg.Any<CancellationToken>()).Returns(inv);

        var result = await _handler.Handle(new GetInvoiceByIdQuery(inv.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(inv.Id);
        result.Value.InvoiceNumber.Should().Be("INV-9");
        result.Value.Amount.Should().Be(105m);
        result.Value.Status.Should().Be("Issued");
    }
}
