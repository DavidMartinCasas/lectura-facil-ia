// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Users;

/// <summary>
/// Usuario de prueba que se crea al arrancar si no existe (<c>SEED__TESTUSER__EMAIL</c> y
/// <c>SEED__TESTUSER__PASSWORD</c>). Opcional: sin los dos valores no se crea nada. La contraseña nunca va en el
/// repositorio.
/// </summary>
public sealed class TestUserOptions : IValidatableObject
{
    public const string SectionName = "Seed:TestUser";

    /// <summary>Longitud máxima de un correo (RFC 5321) y de la columna de Identity.</summary>
    public const int MaxEmailLength = 256;

    public const int MaxPasswordLength = 128;

    public string? Email { get; set; }

    [Secret]
    public string? Password { get; set; }

    public bool IsConfigured => HasEmail && HasPassword;

    private bool HasEmail => !string.IsNullOrWhiteSpace(Email);

    private bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HasEmail != HasPassword)
        {
            yield return new ValidationResult(
                "Seed:TestUser necesita Email y Password, o ninguno de los dos.",
                [nameof(Email), nameof(Password)]);
            yield break;
        }

        if (!HasEmail)
        {
            yield break;
        }

        if (Email!.Length > MaxEmailLength || !new EmailAddressAttribute().IsValid(Email))
        {
            yield return new ValidationResult("Seed:TestUser:Email no es un correo válido.", [nameof(Email)]);
        }

        if (Password!.Length > MaxPasswordLength)
        {
            yield return new ValidationResult(
                $"Seed:TestUser:Password no puede tener más de {MaxPasswordLength} caracteres.",
                [nameof(Password)]);
        }
    }
}
