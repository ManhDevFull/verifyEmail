using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Verify.Models;

namespace Verify.Validation;

public static class WelcomeEmailRequestValidator
{
    private static readonly EmailAddressAttribute EmailAddressAttribute = new();

    public static bool TryValidate(WelcomeEmailRequest? request, out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(4, StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors[string.Empty] = new[] { "Request body is required." };
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(WelcomeEmailRequest.Email)] = new[] { "Email is required." };
        }
        else if (!EmailAddressAttribute.IsValid(request.Email))
        {
            errors[nameof(WelcomeEmailRequest.Email)] = new[] { "Email format is invalid." };
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            errors[nameof(WelcomeEmailRequest.Subject)] = new[] { "Subject is required." };
        }

        var hasText = !string.IsNullOrWhiteSpace(request.TextBody);
        var hasHtml = !string.IsNullOrWhiteSpace(request.HtmlBody);
        if (!hasText && !hasHtml)
        {
            errors["Content"] = new[] { "Either TextBody or HtmlBody must be provided." };
        }

        return errors.Count == 0;
    }
}
