using System;
using System.Threading;
using System.Threading.Tasks;

namespace Verify.Data;

public interface IOtpRepository
{
    Task<long> InsertAsync(
        string email,
        string otpHash,
        string type,
        DateTimeOffset expiresAt,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    Task<OtpRecord?> GetLatestActiveAsync(
        string email,
        string type,
        CancellationToken cancellationToken);

    Task MarkUsedAsync(
        long id,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);

    Task IncrementAttemptCountAsync(long id, CancellationToken cancellationToken);

    Task RemoveExpiredAsync(DateTimeOffset utcNow, CancellationToken cancellationToken);

    Task DeleteAllForEmailAsync(string email, string type, CancellationToken cancellationToken);
}
