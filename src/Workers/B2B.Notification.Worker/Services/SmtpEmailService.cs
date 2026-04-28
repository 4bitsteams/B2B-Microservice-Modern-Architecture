using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace B2B.Notification.Worker.Services;

public sealed class SmtpEmailService(
    IOptions<SmtpSettings> smtpSettings,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpSettings _settings = smtpSettings.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(message.From ?? _settings.FromAddress, _settings.FromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = message.IsHtml
            };

            mailMessage.To.Add(message.To);
            await client.SendMailAsync(mailMessage, ct);
            logger.LogInformation("Email sent to {To}, Subject: {Subject}", message.To, message.Subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}", message.To);
            throw;
        }
    }

    public async Task SendTemplatedAsync(string to, string template, object model, CancellationToken ct = default)
    {
        // In production: use a proper template engine (Fluid, RazorLight, etc.)
        var body = $"<p>Template: {template}</p><pre>{System.Text.Json.JsonSerializer.Serialize(model)}</pre>";
        await SendAsync(new EmailMessage(to, template, body), ct);
    }
}

