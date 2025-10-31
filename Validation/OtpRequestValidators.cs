using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Verify.Models;

namespace Verify.Validation;

public static class SendOtpRequestValidator
{
    private static readonly EmailAddressAttribute EmailAddressAttribute = new();

    public static bool TryValidate(SendOtpRequest? request, out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(1, StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors[string.Empty] = new[] { "Request body is required." };
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(SendOtpRequest.Email)] = new[] { "Email is required." };
        }
        else if (!EmailAddressAttribute.IsValid(request.Email))
        {
            errors[nameof(SendOtpRequest.Email)] = new[] { "Email format is invalid." };
        }

        return errors.Count == 0;
    }
}

public static class VerifyOtpRequestValidator
{
    private static readonly EmailAddressAttribute EmailAddressAttribute = new();

    public static bool TryValidate(VerifyOtpRequest? request, out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(2, StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors[string.Empty] = new[] { "Request body is required." };
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(VerifyOtpRequest.Email)] = new[] { "Email is required." };
        }
        else if (!EmailAddressAttribute.IsValid(request.Email))
        {
            errors[nameof(VerifyOtpRequest.Email)] = new[] { "Email format is invalid." };
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors[nameof(VerifyOtpRequest.Code)] = new[] { "OTP code is required." };
        }
        else if (request.Code.Trim().Length != 6 || !IsDigitsOnly(request.Code))
        {
            errors[nameof(VerifyOtpRequest.Code)] = new[] { "OTP code must be a 6-digit number." };
        }

        return errors.Count == 0;
    }

    private static bool IsDigitsOnly(string value)
    {
        foreach (var c in value.Trim())
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
