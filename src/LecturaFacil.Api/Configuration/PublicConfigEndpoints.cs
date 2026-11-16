// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.HumanVerification;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Configuration;

/// <summary>
/// <c>GET /api/config/public</c> (US-16): solo lo que el navegador necesita y puede ver. El aviso configurable, si
/// Turnstile está activo y su clave del sitio (pública). Nunca la clave secreta ni nada de la infraestructura.
/// </summary>
internal static class PublicConfigEndpoints
{
    public const string Path = "/api/config/public";

    public static IEndpointRouteBuilder MapPublicConfigEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, (IOptions<UiOptions> ui, IOptions<TurnstileOptions> turnstile) =>
                TypedResults.Ok(PublicConfigResponse.From(ui.Value, turnstile.Value)))
            .AllowAnonymous()
            .WithTags("Configuración")
            .WithName("GetPublicConfig")
            .WithSummary("Configuración pública de la interfaz: aviso y desafío anti-bots.");
        return endpoints;
    }
}

/// <param name="Notice">Aviso que se muestra en todas las pantallas. Vacío: sin aviso.</param>
/// <param name="TurnstileEnabled">Si el envío de documentos exige el desafío de Turnstile.</param>
/// <param name="TurnstileSiteKey">Clave pública del widget; vacía si Turnstile está deshabilitado.</param>
internal sealed record PublicConfigResponse(string Notice, bool TurnstileEnabled, string TurnstileSiteKey)
{
    public static PublicConfigResponse From(UiOptions ui, TurnstileOptions turnstile) =>
        new(ui.Notice, turnstile.Enabled, turnstile.Enabled ? turnstile.SiteKey : string.Empty);
}
