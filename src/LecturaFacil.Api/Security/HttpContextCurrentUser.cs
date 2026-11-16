// SPDX-License-Identifier: Apache-2.0
using System.Security.Claims;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Security;

/// <summary>
/// Usuario autenticado de la petición: su id de Identity es el propietario de sus documentos. Todas las rutas que lo usan
/// exigen sesión, así que sin usuario es un error de programación, no un caso esperado.
/// </summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public OwnerId OwnerId =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } value
        && Guid.TryParse(value, out var id)
        && id != Guid.Empty
            ? OwnerId.From(id)
            : throw new InvalidOperationException("The request has no authenticated user.");
}
