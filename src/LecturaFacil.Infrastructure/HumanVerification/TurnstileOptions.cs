// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Infrastructure.HumanVerification;

/// <summary>
/// Cloudflare Turnstile (US-16). Deshabilitado por defecto: necesita una cuenta de Cloudflare. Si se habilita, las dos
/// claves son obligatorias y la API no arranca sin ellas.
/// </summary>
public sealed class TurnstileOptions : IValidatableObject
{
    public const string SectionName = "Turnstile";

    public bool Enabled { get; set; }

    /// <summary>Clave del sitio. Es pública: la recibe el navegador para mostrar el widget.</summary>
    [MaxLength(200, ErrorMessage = "Turnstile:SiteKey no puede superar {1} caracteres.")]
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>Clave secreta con la que el servidor valida los tokens. Solo en el entorno, nunca en el navegador.</summary>
    [Secret]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Tiempo máximo de la validación. Si se agota, la petición se rechaza (fallo seguro).</summary>
    [Range(1, 30, ErrorMessage = "Turnstile:TimeoutSeconds debe estar entre {1} y {2}.")]
    public int TimeoutSeconds { get; set; } = 5;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enabled)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(SiteKey))
        {
            yield return new ValidationResult("Turnstile:SiteKey es obligatoria si Turnstile:Enabled es true.", [nameof(SiteKey)]);
        }

        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            yield return new ValidationResult("Turnstile:SecretKey es obligatoria si Turnstile:Enabled es true.", [nameof(SecretKey)]);
        }
    }
}
