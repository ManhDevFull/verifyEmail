using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Verify.Services;

namespace Verify.Email;

public sealed class MailjetEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly MailjetOptions _options;
    private readonly ILogger<MailjetEmailSender> _logger;

    public MailjetEmailSender(HttpClient httpClient, IOptions<MailjetOptions> options, ILogger<MailjetEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri("https://api.mailjet.com/");
        }
    }

    public async Task SendOtpAsync(string recipientEmail, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var message = CreateBaseMessage(recipientEmail);
        message["Subject"] = _options.Subject;
        message["TextPart"] = BuildTextBody(code, expiresAt);
        message["HTMLPart"] = BuildHtmlBody(code, expiresAt);
        message["CustomID"] = _options.CustomId;

        await SendAsync(message, "OTP email", recipientEmail, cancellationToken);
    }

    public async Task SendEmailAsync(string recipientEmail, string subject, string? textContent, string? htmlContent, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var message = CreateBaseMessage(recipientEmail);
        message["Subject"] = subject;

        if (!string.IsNullOrWhiteSpace(textContent))
        {
            message["TextPart"] = textContent;
        }

        if (!string.IsNullOrWhiteSpace(htmlContent))
        {
            message["HTMLPart"] = htmlContent;
        }

        message["CustomID"] = _options.CustomId;

        await SendAsync(message, "welcome email", recipientEmail, cancellationToken);
    }

    private static string BuildTextBody(string code, DateTimeOffset expiresAtUtc)
    {
        var expiresAtLocal = expiresAtUtc.ToLocalTime();
        return $"Your verification code is {code}. It expires at {expiresAtLocal:t} on {expiresAtLocal:d}.";
    }

    private static string BuildHtmlBody(string code, DateTimeOffset expiresAtUtc)
    {
        var expiresAtLocal = expiresAtUtc.ToLocalTime();
        var expireDate = expiresAtLocal.ToString("d", CultureInfo.CurrentCulture);
        var expireTime = expiresAtLocal.ToString("t", CultureInfo.CurrentCulture);

        return $"""
        <div style="font-family:Arial,sans-serif;font-size:16px;line-height:1.5">
            <p>Your verification code is:</p>
            <p style="font-size:28px;font-weight:bold;letter-spacing:4px">{code}</p>
            <p>This code expires at {expireTime} on {expireDate}. If you did not request this code, you can safely ignore this email.</p>
        </div>
        """;
    }

    private JObject CreateBaseMessage(string recipientEmail)
    {
        return new JObject
        {
            ["From"] = new JObject
            {
                ["Email"] = _options.FromEmail,
                ["Name"] = _options.FromName
            },
            ["To"] = new JArray
            {
                new JObject
                {
                    ["Email"] = recipientEmail
                }
            }
        };
    }

    private async Task SendAsync(JObject message, string operationLabel, string recipientEmail, CancellationToken cancellationToken)
    {
        var payload = new JObject
        {
            ["Messages"] = new JArray { message }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3.1/send");
        var credentialBytes = Encoding.ASCII.GetBytes($"{_options.ApiKey}:{_options.ApiSecret}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Mailjet request failed while sending {Operation} to {Email}", operationLabel, recipientEmail);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Mailjet responded with status {Status} and content {Content} for {Operation} to {Email}", response.StatusCode, responseBody, operationLabel, recipientEmail);
            throw new InvalidOperationException($"Mailjet rejected the {operationLabel} request.");
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException("Mailjet credentials are not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Mailjet FromEmail is not configured.");
        }
    }
}
