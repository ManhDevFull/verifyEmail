using Verify.Models;

namespace Verify.Services;

public interface IOtpService
{
    Task<DateTimeOffset> SendOtpAsync(string email, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OtpVerificationResult> VerifyOtpAsync(string email, string code, CancellationToken cancellationToken);
}
