using System;

namespace Verify.Data;

public sealed record OtpRecord(
    long Id,
    string Email,
    string OtpHash,
    string Type,
    DateTimeOffset ExpiresAt,
    bool Used,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UsedAt,
    string? IpRequest,
    string? UserAgent);
