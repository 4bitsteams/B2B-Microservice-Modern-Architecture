using B2B.Payment.Domain.Entities;
using B2B.Payment.Domain.Events;
using FluentAssertions;
using Xunit;
using PaymentEntity = B2B.Payment.Domain.Entities.Payment;

namespace B2B.Payment.Tests.Domain;

public sealed class PaymentTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static PaymentEntity NewPending(decimal amount = 100m, string currency = "USD") =>
        PaymentEntity.Create(OrderId, CustomerId, TenantId, amount, currency, PaymentMethod.CreditCard);

    [Fact]
    public void Create_ShouldInitializePending()
    {
        var p = NewPending();

        p.Status.Should().Be(PaymentStatus.Pending);
        p.OrderId.Should().Be(OrderId);
        p.Amount.Should().Be(100m);
        p.Currency.Should().Be("USD");
        p.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldUppercaseCurrency()
    {
        PaymentEntity.Create(OrderId, CustomerId, TenantId, 50m, "eur", PaymentMethod.BankTransfer)
            .Currency.Should().Be("EUR");
    }

    [Fact]
    public void Create_ShouldRaisePaymentCreatedEvent()
    {
        var p = NewPending();

        p.DomainEvents.Should().ContainSingle(e => e is PaymentCreatedEvent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveAmount_ShouldThrow(decimal amount)
    {
        var act = () => PaymentEntity.Create(OrderId, CustomerId, TenantId, amount, "USD", PaymentMethod.CreditCard);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_BlankCurrency_ShouldThrow()
    {
        var act = () => PaymentEntity.Create(OrderId, CustomerId, TenantId, 50m, "", PaymentMethod.CreditCard);

        act.Should().Throw<ArgumentException>();
    }

    // ── Process ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Process_ShouldTransitionToCompleted()
    {
        var p = NewPending();
        p.ClearDomainEvents();

        p.Process("TXN-001");

        p.Status.Should().Be(PaymentStatus.Completed);
        p.TransactionReference.Should().Be("TXN-001");
        p.ProcessedAt.Should().NotBeNull();
        p.DomainEvents.Should().ContainSingle(e => e is PaymentProcessedEvent);
    }

    [Fact]
    public void Process_AlreadyCompleted_ShouldThrow()
    {
        var p = NewPending();
        p.Process("TXN-001");

        var act = () => p.Process("TXN-002");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Process_BlankReference_ShouldThrow()
    {
        var p = NewPending();

        var act = () => p.Process("");

        act.Should().Throw<ArgumentException>();
    }

    // ── Fail ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_ShouldTransitionToFailedAndRaiseEvent()
    {
        var p = NewPending();
        p.ClearDomainEvents();

        p.Fail("Card declined");

        p.Status.Should().Be(PaymentStatus.Failed);
        p.FailureReason.Should().Be("Card declined");
        p.DomainEvents.Should().ContainSingle(e => e is PaymentFailedEvent);
    }

    [Fact]
    public void Fail_AfterCompleted_ShouldThrow()
    {
        var p = NewPending();
        p.Process("TXN");

        var act = () => p.Fail("late failure");

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Refund ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Refund_OnCompleted_ShouldTransitionAndRaiseEvent()
    {
        var p = NewPending();
        p.Process("TXN");
        p.ClearDomainEvents();

        p.Refund();

        p.Status.Should().Be(PaymentStatus.Refunded);
        p.DomainEvents.Should().ContainSingle(e => e is PaymentRefundedEvent);
    }

    [Fact]
    public void Refund_OnPending_ShouldThrow()
    {
        var p = NewPending();

        var act = () => p.Refund();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Refund_OnFailed_ShouldThrow()
    {
        var p = NewPending();
        p.Fail("declined");

        var act = () => p.Refund();

        act.Should().Throw<InvalidOperationException>();
    }
}
