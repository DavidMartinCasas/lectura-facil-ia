// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Users;

/// <summary>Crea el usuario de prueba de <see cref="TestUserOptions"/> solo si no existe. Se puede ejecutar en cada arranque.</summary>
public sealed class SeedTestUser(IUserAccounts accounts, IOptions<TestUserOptions> options)
{
    public async Task<Result<SeedOutcome>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var testUser = options.Value;
        if (!testUser.IsConfigured)
        {
            return Result<SeedOutcome>.Success(SeedOutcome.NotConfigured);
        }

        var email = testUser.Email!.Trim();
        if (await accounts.ExistsAsync(email, cancellationToken))
        {
            return Result<SeedOutcome>.Success(SeedOutcome.AlreadyExists);
        }

        var created = await accounts.CreateAsync(email, testUser.Password!, cancellationToken);
        return created.IsSuccess
            ? Result<SeedOutcome>.Success(SeedOutcome.Created)
            : Result<SeedOutcome>.Failure(created.Error!);
    }
}
