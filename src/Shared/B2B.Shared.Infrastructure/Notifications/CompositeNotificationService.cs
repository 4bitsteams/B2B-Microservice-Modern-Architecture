using Microsoft.Extensions.Logging;
using B2B.Shared.Core.Interfaces;

namespace B2B.Shared.Infrastructure.Notifications;

/// <summary>
/// Composite notification service that fans out a message to all registered
/// channel handlers in parallel.
///
/// Register channel-specific handlers in DI:
/// <code>
/// services.AddScoped&lt;INotificationChannelHandler, EmailNotificationHandler&gt;();
/// services.AddScoped&lt;INotificationChannelHandler, SmsNotificationHandler&gt;();
/// </code>
///
/// Each handler declares which channels it supports via <see cref="INotificationChannelHandler.SupportedChannels"/>.
/// The composite fans the message to all handlers whose supported channels
/// intersect with the message's requested channels. Failures in one channel
/// are logged but do not prevent delivery to other channels.
/// </summary>
public sealed class CompositeNotificationService(
    IEnumerable<INotificationChannelHandler> handlers,
    ILogger<CompositeNotificationService> logger)
    : INotificationService
{
    public async Task SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var tasks = handlers
            .Where(h => h.SupportedChannels.Any(c => message.Channels.Contains(c)))
            .Select(h => SendSafeAsync(h, message, ct));

        await Task.WhenAll(tasks);
    }

    private async Task SendSafeAsync(
        INotificationChannelHandler handler,
        NotificationMessage message,
        CancellationToken ct)
    {
        try
        {
            await handler.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Notification channel {Handler} failed for recipient {Recipient}",
                handler.GetType().Name, message.RecipientId);
            // Do not rethrow — other channels should still attempt delivery.
        }
    }
}

/// <summary>Single-channel notification handler. Implement one per channel.</summary>
public interface INotificationChannelHandler
{
    IReadOnlyList<NotificationChannel> SupportedChannels { get; }
    Task SendAsync(NotificationMessage message, CancellationToken ct = default);
}
