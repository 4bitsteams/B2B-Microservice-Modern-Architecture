using FluentAssertions;
using Xunit;
using B2B.Order.Domain.ValueObjects;

namespace B2B.Order.Tests.Domain;

public sealed class AddressTests
{
    // ── Create ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_ShouldSetProperties()
    {
        var address = Address.Create("123 Main St", "New York", "NY", "10001", "US");

        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("New York");
        address.State.Should().Be("NY");
        address.PostalCode.Should().Be("10001");
        address.Country.Should().Be("US");
    }

    [Theory]
    [InlineData("", "City", "US")]
    [InlineData("   ", "City", "US")]
    public void Create_WithBlankStreet_ShouldThrow(string street, string city, string country)
    {
        var act = () => Address.Create(street, city, "ST", "00000", country);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Street", "", "US")]
    [InlineData("Street", "   ", "US")]
    public void Create_WithBlankCity_ShouldThrow(string street, string city, string country)
    {
        var act = () => Address.Create(street, city, "ST", "00000", country);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Street", "City", "")]
    [InlineData("Street", "City", "   ")]
    public void Create_WithBlankCountry_ShouldThrow(string street, string city, string country)
    {
        var act = () => Address.Create(street, city, "ST", "00000", country);

        act.Should().Throw<ArgumentException>();
    }

    // ── Equality ────────────────────────────────────────────────────────────────

    [Fact]
    public void TwoAddresses_WithSameData_ShouldBeEqual()
    {
        var a1 = Address.Create("123 Main St", "New York", "NY", "10001", "US");
        var a2 = Address.Create("123 Main St", "New York", "NY", "10001", "US");

        a1.Should().Be(a2);
    }

    [Fact]
    public void TwoAddresses_WithDifferentData_ShouldNotBeEqual()
    {
        var a1 = Address.Create("123 Main St", "New York", "NY", "10001", "US");
        var a2 = Address.Create("456 Oak Ave", "Boston", "MA", "02101", "US");

        a1.Should().NotBe(a2);
    }

    // ── ToString ────────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_ShouldReturnSingleLineFormat()
    {
        var address = Address.Create("123 Main St", "New York", "NY", "10001", "US");

        var text = address.ToString();

        text.Should().Be("123 Main St, New York, NY 10001, US");
    }
}
