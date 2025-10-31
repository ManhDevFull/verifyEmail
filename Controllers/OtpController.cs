using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Verify.Models;
using Verify.Services;
using Verify.Validation;

namespace Verify.Controllers;

[ApiController]
[Route("otp")]
[Produces("application/json")]
public class OtpController : ControllerBase
{
    private readonly IOtpService _otpService;
    private readonly ILogger<OtpController> _logger;

    public OtpController(IOtpService otpService, ILogger<OtpController> logger)
    {
        _otpService = otpService;
        _logger = logger;
    }

    [HttpPost("send")]
    [ProducesResponseType(typeof(OtpSendResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OtpSendResponse>> SendOtp([FromBody] SendOtpRequest? request, CancellationToken cancellationToken)
    {
        if (!SendOtpRequestValidator.TryValidate(request, out var validationProblem))
        {
            return ValidationProblem(new ValidationProblemDetails(validationProblem));
        }

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();
            var expiresAt = await _otpService.SendOtpAsync(request!.Email, ipAddress, userAgent, cancellationToken);
            var response = new OtpSendResponse(request.Email, expiresAt);
            return Accepted("/otp/verify", response);
        }
        catch (OtpRateLimitExceededException ex)
        {
            _logger.LogWarning(ex, "OTP daily quota exceeded for {Email}", request!.Email);
            return Problem(
                title: "OTP request limit reached.",
                detail: ex.Message,
                statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Unable to send OTP email for {Email}", request!.Email);
            return Problem(
                title: "Failed to send OTP email.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure while sending OTP email for {Email}", request!.Email);
            return Problem(
                title: "Unexpected error while sending OTP.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("verify")]
    [ProducesResponseType(typeof(OtpVerifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OtpVerifyResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OtpVerifyResponse>> VerifyOtp([FromBody] VerifyOtpRequest? request, CancellationToken cancellationToken)
    {
        if (!VerifyOtpRequestValidator.TryValidate(request, out var validationProblem))
        {
            return ValidationProblem(new ValidationProblemDetails(validationProblem));
        }

        var result = await _otpService.VerifyOtpAsync(request!.Email, request.Code, cancellationToken);
        if (result.IsValid)
        {
            return Ok(new OtpVerifyResponse(request.Email, true, null));
        }

        return BadRequest(new OtpVerifyResponse(request.Email, false, result.Error));
    }
}
