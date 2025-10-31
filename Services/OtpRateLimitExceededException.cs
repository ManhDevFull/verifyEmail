using System;

namespace Verify.Services;

public sealed class OtpRateLimitExceededException : Exception
{
    public OtpRateLimitExceededException(string message) : base(message)
    {
    }
}
