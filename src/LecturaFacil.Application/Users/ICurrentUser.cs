// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Users;

/// <summary>Usuario que hace la petición. Los documentos que crea quedan asociados a él.</summary>
public interface ICurrentUser
{
    OwnerId OwnerId { get; }
}
