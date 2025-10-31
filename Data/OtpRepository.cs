using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Verify.Data;

public sealed class OtpRepository : IOtpRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public OtpRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<long> InsertAsync(
        string email,
        string otpHash,
        string type,
        DateTimeOffset expiresAt,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
        """
        INSERT INTO email_verification (email, otp_hash, type, expires_at, ip_request, user_agent)
        VALUES (@email, @otp_hash, @type, @expires_at, @ip_request, @user_agent)
        RETURNING id;
        """;
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("otp_hash", otpHash);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("expires_at", expiresAt.UtcDateTime);
        command.Parameters.Add(new NpgsqlParameter("ip_request", DbType.String) { Value = (object?)ipAddress ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("user_agent", DbType.String) { Value = (object?)userAgent ?? DBNull.Value });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            throw new InvalidOperationException("Failed to insert OTP record.");
        }

        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<OtpRecord?> GetLatestActiveAsync(string email, string type, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
        """
        SELECT id,
               email,
               otp_hash,
               type,
               expires_at,
               used,
               attempt_count,
               created_at,
               used_at,
               ip_request,
               user_agent
        FROM email_verification
        WHERE email = @email
          AND type = @type
          AND used = FALSE
        ORDER BY created_at DESC
        LIMIT 1;
        """;
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("type", type);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task MarkUsedAsync(long id, DateTimeOffset usedAt, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
        """
        UPDATE email_verification
        SET used = TRUE,
            used_at = @used_at
        WHERE id = @id;
        """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("used_at", usedAt.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task IncrementAttemptCountAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
        """
        UPDATE email_verification
        SET attempt_count = attempt_count + 1
        WHERE id = @id;
        """;
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
        """
        DELETE FROM email_verification
        WHERE id = @id;
        """;
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveExpiredAsync(DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
        """
        DELETE FROM email_verification
        WHERE used = FALSE
          AND expires_at <= @threshold;
        """;
        command.Parameters.AddWithValue("threshold", utcNow.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAllForEmailAsync(string email, string type, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
        """
        DELETE FROM email_verification
        WHERE email = @email
          AND type = @type;
        """;
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("type", type);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static OtpRecord Map(NpgsqlDataReader reader)
    {
        var id = reader.GetInt64(0);
        var email = reader.GetString(1);
        var otpHash = reader.GetString(2);
        var type = reader.GetString(3);
        var expiresAt = ToUtc(reader.GetDateTime(4));
        var used = reader.GetBoolean(5);
        var attemptCount = reader.GetInt32(6);
        var createdAt = ToUtc(reader.GetDateTime(7));
        var usedAt = reader.IsDBNull(8) ? (DateTimeOffset?)null : ToUtc(reader.GetDateTime(8));
        var ipRequest = reader.IsDBNull(9) ? null : reader.GetString(9);
        var userAgent = reader.IsDBNull(10) ? null : reader.GetString(10);

        return new OtpRecord(id, email, otpHash, type, expiresAt, used, attemptCount, createdAt, usedAt, ipRequest, userAgent);
    }

    private static DateTimeOffset ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Unspecified => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
            DateTimeKind.Utc => new DateTimeOffset(value),
            DateTimeKind.Local => new DateTimeOffset(value.ToUniversalTime(), TimeSpan.Zero),
            _ => new DateTimeOffset(value)
        };
    }
}
