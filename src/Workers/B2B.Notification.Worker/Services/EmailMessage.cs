namespace B2B.Notification.Worker.Services;

public sealed record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsHtml = true,
    string? From = null);
