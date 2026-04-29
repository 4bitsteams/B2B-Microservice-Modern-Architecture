using B2B.Review.Domain.Events;
using B2B.Shared.Core.Domain;
using B2B.Shared.Core.Interfaces;

namespace B2B.Review.Domain.Entities;

public sealed class Review : AggregateRoot<Guid>, IAuditableEntity
{
    public Guid ProductId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? OrderId { get; private set; }
    public int Rating { get; private set; }
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public ReviewStatus Status { get; private set; }
    public bool IsVerifiedPurchase { get; private set; }
    public int HelpfulVotes { get; private set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private Review() { }

    public static Review Submit(Guid productId, Guid customerId, Guid tenantId,
        int rating, string title, string body, Guid? orderId = null, bool isVerifiedPurchase = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            CustomerId = customerId,
            TenantId = tenantId,
            OrderId = orderId,
            Rating = rating,
            Title = title,
            Body = body,
            Status = ReviewStatus.Pending,
            IsVerifiedPurchase = isVerifiedPurchase
        };

        review.RaiseDomainEvent(new ReviewSubmittedEvent(review.Id, productId, customerId, rating));
        return review;
    }

    public void Approve()
    {
        if (Status != ReviewStatus.Pending)
            throw new InvalidOperationException($"Cannot approve review in status '{Status}'.");
        Status = ReviewStatus.Approved;
        RaiseDomainEvent(new ReviewApprovedEvent(Id, ProductId));
    }

    public void Reject(string reason)
    {
        if (Status != ReviewStatus.Pending)
            throw new InvalidOperationException($"Cannot reject review in status '{Status}'.");
        Status = ReviewStatus.Rejected;
    }

    public void MarkHelpful() => HelpfulVotes++;
}

public enum ReviewStatus { Pending, Approved, Rejected }
