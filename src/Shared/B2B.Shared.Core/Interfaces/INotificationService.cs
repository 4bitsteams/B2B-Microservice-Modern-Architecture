namespace B2B.Shared.Core.Interfaces;

/// <summary>
/// Multi-channel notification abstraction.
///
/// The Notification Worker currently only sends SMTP email via <c>SmtpEmailService</c>.
/// This interface unifies Email, SMS, and Push under one contract so that:
///   • Application handlers dispatch a <see cref="NotificationMessage"/> without
///     caring which channel delivers it.
///   • New channels (Slack, Teams, WhatsApp) are added as new implementations
///     registered alongside existing ones — handlers are untouched.
///   • In tests, a no-op implementation can be substituted trivially.
/// </summary>
public interface INotificationService
{
    Task SendAsync(NotificationMessage message, CancellationToken ct = default);
}

/// <summary>Notification channels supported by the platform.</summary>
public enum NotificationChannel
{
    Email,
    Sms,
    Push,
    Webhook
}

/// <summary>A notification destined for one or more channels.</summary>
public sealed record NotificationMessage(
    string RecipientId,
    string RecipientEmail,
    string? RecipientPhone,
    string Subject,
    string Body,
    NotificationChannel[] Channels,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    /// <summary>Convenience factory for email-only notifications.</summary>
    public static NotificationMessage Email(
        string recipientId, string email, string subject, string body) =>
        new(recipientId, email, null, subject, body, [NotificationChannel.Email]);

    /// <summary>Convenience factory for SMS-only notifications.</summary>
    public static NotificationMessage Sms(
        string recipientId, string email, string phone, string body) =>
        new(recipientId, email, phone, string.Empty, body, [NotificationChannel.Sms]);

    /// <summary>Convenience factory for multi-channel notifications.</summary>
    public static NotificationMessage MultiChannel(
        string recipientId, string email, string? phone,
        string subject, string body,
        params NotificationChannel[] channels) =>
        new(recipientId, email, phone, subject, body, channels);
}
