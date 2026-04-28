using B2B.Shared.Core.Domain;

namespace B2B.Product.Domain.ValueObjects;

/// <summary>
/// Immutable value object that represents a monetary amount in a specific currency.
///
/// Amounts are always rounded to 2 decimal places on construction.
/// Currency codes are normalised to uppercase (e.g. <c>"usd"</c> → <c>"USD"</c>).
/// Arithmetic operations enforce currency homogeneity — adding or subtracting
/// values in different currencies throws <see cref="InvalidOperationException"/>.
///
/// Create instances via the factory methods:
/// <code>
/// var price    = Money.Of(99.99m, "USD");
/// var free     = Money.Zero("EUR");
/// var subtotal = price.Multiply(3);      // 299.97 USD
/// var total    = subtotal.Add(tax);      // same currency required
/// </code>
/// </summary>
public sealed class Money : ValueObject
{
    /// <summary>The monetary amount, rounded to 2 decimal places.</summary>
    public decimal Amount { get; }

    /// <summary>ISO 4217 currency code in uppercase (e.g. <c>"USD"</c>, <c>"EUR"</c>).</summary>
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    /// <summary>
    /// Creates a <see cref="Money"/> value with the given <paramref name="amount"/>
    /// and <paramref name="currency"/> code.
    /// </summary>
    /// <param name="amount">Non-negative monetary amount.</param>
    /// <param name="currency">ISO 4217 currency code. Defaults to <c>"USD"</c>.</param>
    public static Money Of(decimal amount, string currency = "USD") => new(amount, currency);

    /// <summary>Creates a zero-amount <see cref="Money"/> value in the given <paramref name="currency"/>.</summary>
    public static Money Zero(string currency = "USD") => new(0, currency);

    /// <summary>
    /// Returns a new <see cref="Money"/> value equal to the sum of this and
    /// <paramref name="other"/>. Both operands must be in the same currency.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when currencies differ.</exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Returns a new <see cref="Money"/> value equal to this minus <paramref name="other"/>.
    /// Both operands must be in the same currency.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when currencies differ.</exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Returns a new <see cref="Money"/> value equal to this amount multiplied by <paramref name="quantity"/>.</summary>
    public Money Multiply(int quantity) => new(Amount * quantity, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot operate on different currencies: {Currency} vs {other.Currency}");
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    /// <summary>Returns a human-readable representation, e.g. <c>"99.99 USD"</c>.</summary>
    public override string ToString() => $"{Amount:F2} {Currency}";
}
