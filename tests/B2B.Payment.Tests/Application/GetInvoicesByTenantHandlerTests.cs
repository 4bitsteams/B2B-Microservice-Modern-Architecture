using B2B.Payment.Application.Interfaces;
using B2B.Payment.Application.Queries.GetInvoicesByTenant;
using B2B.Payment.Domain.Entities;
using B2B.Shared.Core.Common;
using B2B.Shared.Core.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace B2B.Payment.Tests.Application;

public sealed class GetInvoicesByTenantHandlerTests
{
    private readonly IReadInvoiceRepository _repo = Substitute.For<IReadInvoiceRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly GetInvoicesByTenantHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public GetInvoicesByTenantHandlerTests()
    {
        _currentUser.TenantId.Returns(TenantId);
        _handler = new GetInvoicesByTenantHandler(_repo, _currentUser);
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedInvoices()
    {
        var inv = Invoice.Create("INV-1", Guid.NewGuid(), Guid.NewGuid(), TenantId, 100m, 0m, "USD");
        var paged = PagedList<Invoice>.Create(new[] { inv }, 1, 20);
        _repo.GetPagedByTenantAsync(TenantId, 1, 20, Arg.Any<CancellationToken>()).Returns(paged);

        var result = await _handler.Handle(new GetInvoicesByTenantQuery(1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].InvoiceNumber.Should().Be("INV-1");
    }

    [Fact]
    public async Task Handle_ShouldQueryCurrentTenant()
    {
        _repo.GetPagedByTenantAsync(TenantId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(PagedList<Invoice>.Create(Array.Empty<Invoice>(), 1, 20));

        await _handler.Handle(new GetInvoicesByTenantQuery(), default);

        await _repo.Received(1).GetPagedByTenantAsync(TenantId, 1, 20, Arg.Any<CancellationToken>());
    }
}
