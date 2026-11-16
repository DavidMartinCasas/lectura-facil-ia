// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using Microsoft.AspNetCore.Identity;

namespace LecturaFacil.Infrastructure.Identity;

/// <summary>Cuentas guardadas con ASP.NET Core Identity: la contraseña se guarda con el hash de Identity.</summary>
public sealed class IdentityUserAccounts(UserManager<ApplicationUser> users) : IUserAccounts
{
    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await users.FindByEmailAsync(email) is not null;
    }

    public async Task<Result> CreateAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, password);

        // Solo los códigos de Identity (PasswordTooShort…): sus descripciones podrían incluir el correo.
        return created.Succeeded
            ? Result.Success()
            : Result.Failure(UserErrors.SeedFailed(created.Errors.Select(error => error.Code)));
    }
}
