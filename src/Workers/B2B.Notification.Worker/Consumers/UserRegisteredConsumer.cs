using MassTransit;
using B2B.Notification.Worker.Contracts;
using B2B.Notification.Worker.Services;

namespace B2B.Notification.Worker.Consumers;

public sealed class UserRegisteredConsumer(
    IEmailService emailService,
    ILogger<UserRegisteredConsumer> logger)
    : IConsumer<UserRegisteredIntegration>
{
    public async Task Consume(ConsumeContext<UserRegisteredIntegration> context)
    {
        var evt = context.Message;
        var correlationId = ConsumerCorrelationHelper.Restore(context);

        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["Email"]         = evt.Email
               }))
        {
            logger.LogInformation("Sending welcome email to {Email}", evt.Email);

            var body = $"""
                <h2>Welcome to B2B Platform, {evt.FullName}!</h2>
                <p>Your account has been successfully created.</p>
                <p>You can now log in and start managing your business.</p>
                <p><em>The B2B Platform Team</em></p>
                """;

            await emailService.SendAsync(new EmailMessage(
                To: evt.Email,
                Subject: "Welcome to B2B Platform!",
                Body: body), context.CancellationToken);
        }
    }
}
