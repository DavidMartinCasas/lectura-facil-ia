// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;

namespace LecturaFacil.Infrastructure.Tests;

/// <summary>Valida como lo hace <c>ValidateDataAnnotations</c> al arrancar y devuelve los mensajes.</summary>
internal static class OptionsValidation
{
    public static List<string> Validate(object options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return [.. results.Select(result => result.ErrorMessage ?? string.Empty)];
    }
}
