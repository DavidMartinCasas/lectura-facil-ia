// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Security;

/// <summary>
/// Propietario fijo mientras no haya autenticación: todos los documentos quedan asociados a él.
/// TODO(US-15): sustituir por el usuario autenticado.
/// </summary>
internal sealed class FixedCurrentUser : ICurrentUser
{
    public static readonly OwnerId DevelopmentOwner = OwnerId.From(new Guid("00000000-0000-0000-0000-000000000001"));

    public OwnerId OwnerId => DevelopmentOwner;
}
