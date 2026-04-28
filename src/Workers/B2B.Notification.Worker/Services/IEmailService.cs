namespace B2B.Notification.Worker.Services;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
    Task SendTemplatedAsync(string to, string template, object model, CancellationToken ct = default);
}
