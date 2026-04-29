using B2B.Review.Application.Commands.SubmitReview;
using FluentAssertions;
using Xunit;

namespace B2B.Review.Tests.Application.Validators;

public sealed class SubmitReviewValidatorTests
{
    private readonly SubmitReviewValidator _validator = new();

    private static SubmitReviewCommand Valid() =>
        new(Guid.NewGuid(), 5, "Great", "Loved it");

    [Fact]
    public void Valid_ShouldPass() => _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void EmptyProductId_ShouldFail() =>
        _validator.Validate(Valid() with { ProductId = Guid.Empty }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void RatingOutOfRange_ShouldFail(int r) =>
        _validator.Validate(Valid() with { Rating = r }).IsValid.Should().BeFalse();

    [Fact]
    public void EmptyTitle_ShouldFail() =>
        _validator.Validate(Valid() with { Title = "" }).IsValid.Should().BeFalse();

    [Fact]
    public void TooLongTitle_ShouldFail() =>
        _validator.Validate(Valid() with { Title = new string('a', 201) }).IsValid.Should().BeFalse();

    [Fact]
    public void EmptyBody_ShouldFail() =>
        _validator.Validate(Valid() with { Body = "" }).IsValid.Should().BeFalse();

    [Fact]
    public void TooLongBody_ShouldFail() =>
        _validator.Validate(Valid() with { Body = new string('a', 2001) }).IsValid.Should().BeFalse();
}
