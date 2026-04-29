using B2B.Payment.Domain.Entities;
using B2B.Payment.Domain.Events;
using FluentAssertions;
using Xunit;

namespace B2B.Payment.Tests.Domain;

public sealed class InvoiceTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Invoice New(decimal subtotal = 100m, decimal tax = 7m,
        string currency = "USD", int netDays = 30) =>
        Invoice.Create("INV-001", OrderId, CustomerId, TenantId, subtotal, tax, currency, netDays);

    [Fact]
    public void Create_ShouldComputeTotalAndStatusIssued()
    {
        var inv = New(100m, 7m);

        inv.Subtotal.Should().Be(100m);
        inv.TaxAmount.Should().Be(7m);
        inv.TotalAmount.Should().Be(107m);
        inv.Status.Should().Be(InvoiceStatus.Issued);
    }

    [Fact]
    public void Create_ShouldUppercaseCurrency()
    {
        New(currency: "eur").Currency.Should().Be("EUR");
    }

    [Fact]
    public void Create_ShouldStampDueDate()
    {
        var inv = New(netDays: 60);

        inv.DueAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(60), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Create_ShouldRaiseInvoiceIssuedEvent()
    {
        var inv = New();

        inv.DomainEvents.Should().ContainSingle(e => e is InvoiceIssuedEvent);
    }

    [Fact]
    public void Create_BlankInvoiceNumber_ShouldThrow()
    {
        var act = () => Invoice.Create("", OrderId, CustomerId, TenantId, 10m, 0m, "USD");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NegativeSubtotal_ShouldThrow()
    {
        var act = () => Invoice.Create("INV", OrderId, CustomerId, TenantId, -1m, 0m, "USD");

        act.Should().Throw<ArgumentException>();
    }

    // ── MarkPaid ────────────────────────────────────────────────────────────────

    [Fact]
    public void MarkPaid_ShouldTransitionAndStampPaidAt()
    {
        var inv = New();
        inv.ClearDomainEvents();

        inv.MarkPaid("PAY-001");

        inv.Status.Should().Be(InvoiceStatus.Paid);
        inv.PaidAt.Should().NotBeNull();
        inv.DomainEvents.Should().ContainSingle(e => e is InvoicePaidEvent);
    }

    [Fact]
    public void MarkPaid_AlreadyPaid_ShouldThrow()
    {
        var inv = New();
        inv.MarkPaid("PAY-001");

        var act = () => inv.MarkPaid("PAY-002");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkPaid_AfterCancel_ShouldThrow()
    {
        var inv = New();
        inv.Cancel();

        var act = () => inv.MarkPaid("PAY");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Cancel ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_OnIssued_ShouldTransitionAndRaiseEvent()
    {
        var inv = New();
        inv.ClearDomainEvents();

        inv.Cancel();

        inv.Status.Should().Be(InvoiceStatus.Cancelled);
        inv.DomainEvents.Should().ContainSingle(e => e is InvoiceCancelledEvent);
    }

    [Fact]
    public void Cancel_OnPaid_ShouldThrow()
    {
        var inv = New();
        inv.MarkPaid("PAY");

        var act = () => inv.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── IsOverdue ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_BeforeDue_ShouldBeFalse()
    {
        var inv = New(netDays: 30);

        inv.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_AfterDuePending_ShouldBeTrue()
    {
        var inv = Invoice.Create("INV-PAST", OrderId, CustomerId, TenantId, 100m, 0m, "USD", netTermsDays: -1);

        inv.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_AfterPaid_ShouldBeFalse()
    {
        var inv = Invoice.Create("INV-PAID", OrderId, CustomerId, TenantId, 100m, 0m, "USD", netTermsDays: -1);
        inv.MarkPaid("PAY");

        inv.IsOverdue.Should().BeFalse();
    }
}
