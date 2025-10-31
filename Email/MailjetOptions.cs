namespace Verify.Email;

public sealed class MailjetOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "OTP Service";
    public string Subject { get; set; } = "Your verification code";
    public string CustomId { get; set; } = "OtpVerification";
}
