using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Verify.Data;
using Verify.Models;

namespace Verify.Services;

public sealed class OtpService : IOtpService
{
    private const int OtpLength = 6;
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    // Database enforces known OTP scenarios; reuse existing "register" bucket.
    private const string OtpType = "register";
    private const int DailyOtpLimit = 5;

    private readonly IOtpRepository _repository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<OtpService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IMemoryCache _memoryCache;

    public OtpService(
        IOtpRepository repository,
        IEmailSender emailSender,
        ILogger<OtpService> logger,
        TimeProvider timeProvider,
        IMemoryCache memoryCache)
    {
        _repository = repository;
        _emailSender = emailSender;
        _logger = logger;
        _timeProvider = timeProvider;
        _memoryCache = memoryCache;
    }

    public async Task<DateTimeOffset> SendOtpAsync(string email, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = _timeProvider.GetUtcNow();
        var normalizedEmail = email.Trim();

        await _repository.RemoveExpiredAsync(now, cancellationToken);

        using var emailQuota = ReserveEmailQuota(normalizedEmail, now);
        using var deviceQuota = ReserveDeviceQuota(ipAddress, userAgent, now);

        var code = GenerateOtp();
        var expiresAt = now.Add(OtpLifetime);
        var codeHash = HashCode(code);
        long recordId = 0;

        try
        {
            recordId = await _repository.InsertAsync(normalizedEmail, codeHash, OtpType, expiresAt, ipAddress, userAgent, cancellationToken);
            await _emailSender.SendOtpAsync(normalizedEmail, code, expiresAt, cancellationToken);
            emailQuota.Commit();
            deviceQuota?.Commit();
        }
        catch
        {
            if (recordId != 0)
            {
                try
                {
                    await _repository.DeleteAsync(recordId, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up OTP record {Id} after send failure.", recordId);
                }
            }

            throw;
        }

        _logger.LogInformation("OTP generated for {Email}, expires at {ExpiresAt}", normalizedEmail, expiresAt);

        return expiresAt;
    }

    public Task<OtpVerificationResult> VerifyOtpAsync(string email, string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = email.Trim();
        var normalizedCode = code.Trim();
        return VerifyInternalAsync(normalizedEmail, normalizedCode, cancellationToken);
    }

    private async Task<OtpVerificationResult> VerifyInternalAsync(string email, string code, CancellationToken cancellationToken)
    {
        var record = await _repository.GetLatestActiveAsync(email, OtpType, cancellationToken);
        if (record is null)
        {
            return OtpVerificationResult.Invalid("OTP has expired or was not requested.");
        }

        var now = _timeProvider.GetUtcNow();
        if (record.ExpiresAt <= now)
        {
            await _repository.MarkUsedAsync(record.Id, now, cancellationToken);
            _logger.LogInformation("OTP for {Email} expired at {ExpiresAt}", email, record.ExpiresAt);
            return OtpVerificationResult.Invalid("OTP has expired.");
        }

        var incomingHash = HashCode(code);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(record.OtpHash), Convert.FromHexString(incomingHash)))
        {
            await _repository.IncrementAttemptCountAsync(record.Id, cancellationToken);
            _logger.LogWarning("OTP mismatch for {Email}", email);
            return OtpVerificationResult.Invalid("OTP is incorrect.");
        }

        await _repository.DeleteAsync(record.Id, cancellationToken);
        try
        {
            await _repository.DeleteAllForEmailAsync(email, OtpType, cancellationToken);
        }
        catch (Exception cleanupEx) when (cleanupEx is not OperationCanceledException)
        {
            _logger.LogWarning(cleanupEx, "Failed to remove OTP records for {Email} after successful verification.", email);
        }
        _logger.LogInformation("OTP for {Email} verified successfully", email);
        return OtpVerificationResult.Valid();
    }

    private static string GenerateOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, OtpLength));
        return value.ToString($"D{OtpLength}", CultureInfo.InvariantCulture);
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private QuotaReservation ReserveEmailQuota(string email, DateTimeOffset now)
    {
        var cacheKey = GetEmailQuotaCacheKey(email, now);
        return ReserveQuota(cacheKey, now, "Daily OTP limit reached for this email. Please try again tomorrow.");
    }

    private QuotaReservation? ReserveDeviceQuota(string? ipAddress, string? userAgent, DateTimeOffset now)
    {
        var signature = BuildDeviceSignature(ipAddress, userAgent);
        if (signature is null)
        {
            return null;
        }

        var cacheKey = GetDeviceQuotaCacheKey(signature, now);
        return ReserveQuota(cacheKey, now, "Daily OTP limit reached for this device. Please try again tomorrow.");
    }

    private QuotaReservation ReserveQuota(string cacheKey, DateTimeOffset now, string limitMessage)
    {
        var quota = _memoryCache.GetOrCreate(cacheKey, entry =>
        {
            var nextMidnightUtc = now.UtcDateTime.Date.AddDays(1);
            entry.AbsoluteExpiration = new DateTimeOffset(nextMidnightUtc, TimeSpan.Zero);
            return new OtpSendQuota();
        })!;

        lock (quota)
        {
            if (quota.Count >= DailyOtpLimit)
            {
                throw new OtpRateLimitExceededException(limitMessage);
            }

            quota.Count++;
        }

        return new QuotaReservation(quota);
    }

    private static string GetEmailQuotaCacheKey(string email, DateTimeOffset now)
    {
        var normalizedEmail = email.ToLower(CultureInfo.InvariantCulture);
        var dateStamp = now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"otp-quota:email:{normalizedEmail}:{dateStamp}";
    }

    private static string GetDeviceQuotaCacheKey(string signature, DateTimeOffset now)
    {
        var dateStamp = now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"otp-quota:device:{signature}:{dateStamp}";
    }

    private static string? BuildDeviceSignature(string? ipAddress, string? userAgent)
    {
        var hasIp = !string.IsNullOrWhiteSpace(ipAddress);
        var hasAgent = !string.IsNullOrWhiteSpace(userAgent);
        if (!hasIp && !hasAgent)
        {
            return null;
        }

        var canonical = $"{ipAddress?.Trim() ?? "unknown-ip"}|{userAgent?.Trim() ?? "unknown-agent"}";
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private sealed class OtpSendQuota
    {
        public int Count;
    }

    private sealed class QuotaReservation : IDisposable
    {
        private readonly OtpSendQuota _quota;
        private bool _committed;

        public QuotaReservation(OtpSendQuota quota)
        {
            _quota = quota;
        }

        public void Commit()
        {
            _committed = true;
        }

        public void Dispose()
        {
            if (_committed)
            {
                return;
            }

            lock (_quota)
            {
                if (_quota.Count > 0)
                {
                    _quota.Count--;
                }
            }
        }
    }
}
