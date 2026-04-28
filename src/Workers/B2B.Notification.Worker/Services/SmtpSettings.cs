namespace B2B.Notification.Worker.Services;

public sealed class SmtpSettings
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = "noreply@b2bplatform.com";
    public string FromName { get; init; } = "B2B Platform";
}
