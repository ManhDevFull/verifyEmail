namespace Verify.Models;

public sealed record WelcomeEmailRequest(
    string Email,
    string Subject,
    string? TextBody,
    string? HtmlBody);
