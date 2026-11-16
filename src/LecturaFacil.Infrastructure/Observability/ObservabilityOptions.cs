// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using LecturaFacil.Application.Common;

namespace LecturaFacil.Infrastructure.Observability;

/// <summary>
/// Sentry y versión de la aplicación (US-17). Sin DSN, Sentry queda deshabilitado: por defecto no se envía nada fuera.
/// </summary>
public sealed class ObservabilityOptions : IValidatableObject
{
    public const string SectionName = "Observability";
    public const int MaxNameLength = 64;

    /// <summary>DSN de Sentry del backend. Vacío: deshabilitado.</summary>
    [Secret]
    public string SentryDsn { get; set; } = string.Empty;

    /// <summary>
    /// DSN de Sentry del frontend. Es público por naturaleza (lo ve el navegador), así que no es secreto. Vacío:
    /// deshabilitado. Se entrega al navegador al cargar la página, no al compilar: se inyecta en el despliegue.
    /// </summary>
    public string FrontendSentryDsn { get; set; } = string.Empty;

    /// <summary>Entorno que se envía a Sentry, por ejemplo <c>production</c> o <c>evaluacion</c>.</summary>
    [Required(ErrorMessage = "Observability:Environment es obligatorio.")]
    [RegularExpression(@"^[A-Za-z0-9._-]{1,64}$", ErrorMessage = "Observability:Environment solo admite letras, cifras, '.', '_' y '-' (64 como mucho).")]
    public string Environment { get; set; } = "production";

    /// <summary>Versión (por ejemplo el <c>git sha</c>). Vacía: la versión con la que se compiló la API.</summary>
    [RegularExpression(@"^[A-Za-z0-9.+_-]{0,64}$", ErrorMessage = "Observability:Release solo admite letras, cifras, '.', '+', '_' y '-' (64 como mucho).")]
    public string Release { get; set; } = string.Empty;

    /// <summary>Informes de errores del navegador que la API reenvía a Sentry por minuto y por cliente (IP).</summary>
    [Range(1, 1_000, ErrorMessage = "Observability:FrontendReportsPerMinute debe estar entre {1} y {2}.")]
    public int FrontendReportsPerMinute { get; set; } = 10;

    /// <summary>Tamaño máximo de un informe de errores del navegador, en bytes.</summary>
    [Range(1_024, 1_048_576, ErrorMessage = "Observability:MaxFrontendReportBytes debe estar entre {1} y {2}.")]
    public int MaxFrontendReportBytes { get; set; } = 200_000;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(SentryDsn) && SentryEndpoint.TryParse(SentryDsn) is null)
        {
            yield return new ValidationResult(
                "Observability:SentryDsn no es un DSN de Sentry válido (https://clave@servidor/proyecto).", [nameof(SentryDsn)]);
        }

        if (!string.IsNullOrEmpty(FrontendSentryDsn) && SentryEndpoint.TryParse(FrontendSentryDsn) is null)
        {
            yield return new ValidationResult(
                "Observability:FrontendSentryDsn no es un DSN de Sentry válido (https://clave@servidor/proyecto).", [nameof(FrontendSentryDsn)]);
        }
    }
}
