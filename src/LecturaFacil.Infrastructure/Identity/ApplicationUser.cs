// SPDX-License-Identifier: Apache-2.0
using Microsoft.AspNetCore.Identity;

namespace LecturaFacil.Infrastructure.Identity;

/// <summary>Usuario de ASP.NET Core Identity. Su <c>Id</c> es el <see cref="Domain.Documents.OwnerId"/> de sus documentos.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>;
