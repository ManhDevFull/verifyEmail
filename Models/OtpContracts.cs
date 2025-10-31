namespace Verify.Models;

public sealed record SendOtpRequest(string Email);

public sealed record VerifyOtpRequest(string Email, string Code);

public sealed record OtpSendResponse(string Email, DateTimeOffset ExpiresAt);

public sealed record OtpVerifyResponse(string Email, bool Verified, string? Error);

public readonly record struct OtpVerificationResult(bool IsValid, string? Error)
{
    public static OtpVerificationResult Valid() => new(true, null);

    public static OtpVerificationResult Invalid(string error) => new(false, error);
}
