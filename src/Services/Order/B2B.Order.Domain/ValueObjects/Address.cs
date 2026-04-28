using B2B.Shared.Core.Domain;

namespace B2B.Order.Domain.ValueObjects;

/// <summary>
/// Immutable value object that represents a physical mailing address.
///
/// Equality is component-based: two addresses are equal when all five fields
/// match exactly (street, city, state, postal code, and country). Used for
/// both shipping and billing addresses on <c>Order</c>.
///
/// Create instances via the factory method, which validates required fields:
/// <code>
/// var address = Address.Create("123 Main St", "New York", "NY", "10001", "US");
/// </code>
/// </summary>
public sealed class Address : ValueObject
{
    /// <summary>Street line including house/building number.</summary>
    public string Street { get; }

    /// <summary>City or locality name.</summary>
    public string City { get; }

    /// <summary>State, province, or region code (optional for countries without states).</summary>
    public string State { get; }

    /// <summary>Postal or ZIP code.</summary>
    public string PostalCode { get; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. <c>"US"</c>, <c>"GB"</c>).</summary>
    public string Country { get; }

    private Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    /// <summary>
    /// Creates and validates an <see cref="Address"/>.
    /// </summary>
    /// <param name="street">Required. Street line including building number.</param>
    /// <param name="city">Required. City or locality.</param>
    /// <param name="state">State, province, or region (may be empty for countries without states).</param>
    /// <param name="postalCode">Postal or ZIP code.</param>
    /// <param name="country">Required. ISO 3166-1 alpha-2 country code.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="street"/>, <paramref name="city"/>, or
    /// <paramref name="country"/> is null or whitespace.
    /// </exception>
    public static Address Create(string street, string city, string state, string postalCode, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        return new Address(street, city, state, postalCode, country);
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }

    /// <summary>Returns a single-line address string, e.g. <c>"123 Main St, New York, NY 10001, US"</c>.</summary>
    public override string ToString() => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
