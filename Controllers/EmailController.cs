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
[Route("email")]
[Produces("application/json")]
public class EmailController : ControllerBase
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailController> _logger;

    public EmailController(IEmailSender emailSender, ILogger<EmailController> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpPost("welcome")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendWelcomeEmail([FromBody] WelcomeEmailRequest? request, CancellationToken cancellationToken)
    {
        if (!WelcomeEmailRequestValidator.TryValidate(request, out var validationProblem))
        {
            return ValidationProblem(new ValidationProblemDetails(validationProblem));
        }

        try
        {
            await _emailSender.SendEmailAsync(request!.Email, request.Subject, request.TextBody, request.HtmlBody, cancellationToken);
            return Accepted();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Unable to send welcome email for {Email}", request!.Email);
            return Problem(
                title: "Failed to send welcome email.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure while sending welcome email for {Email}", request!.Email);
            return Problem(
                title: "Unexpected error while sending welcome email.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
