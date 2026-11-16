// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Security;

namespace LecturaFacil.Api.Security;

/// <summary>
/// Filtro reutilizable que exige el token de Turnstile (US-16) en la cabecera <see cref="TokenHeader"/> antes de ejecutar
/// el endpoint. Con Turnstile deshabilitado no pide nada. Si el token falta o no es válido, responde 400 sin leer el
/// cuerpo ni crear nada: no se llega a llamar a ningún LLM.
/// </summary>
internal sealed class HumanVerificationFilter(IHumanVerification verification) : IEndpointFilter
{
    /// <summary>Cabecera con el token del widget. Sirve igual para JSON que para <c>multipart/form-data</c>.</summary>
    public const string TokenHeader = "X-Turnstile-Token";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!verification.IsEnabled)
        {
            return await next(context);
        }

        var httpContext = context.HttpContext;
        var token = httpContext.Request.Headers[TokenHeader].ToString();
        var result = await verification.VerifyAsync(token, httpContext.RequestAborted);
        if (result.IsSuccess)
        {
            return await next(context);
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            detail: result.Error!.Message,
            extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
    }
}

internal static class HumanVerificationFilterExtensions
{
    /// <summary>Exige el desafío anti-bots (si está habilitado) en este endpoint.</summary>
    public static RouteHandlerBuilder RequireHumanVerification(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<HumanVerificationFilter>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
}
