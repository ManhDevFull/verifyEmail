namespace Verify.Services;

public interface IEmailSender
{
    Task SendOtpAsync(string recipientEmail, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task SendEmailAsync(string recipientEmail, string subject, string? textContent, string? htmlContent, CancellationToken cancellationToken);
}
