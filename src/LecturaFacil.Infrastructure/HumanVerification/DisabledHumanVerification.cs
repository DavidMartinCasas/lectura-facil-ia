// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Security;

namespace LecturaFacil.Infrastructure.HumanVerification;

/// <summary>Turnstile deshabilitado (por defecto): no se pide ningún desafío y toda petición pasa.</summary>
public sealed class DisabledHumanVerification : IHumanVerification
{
    public bool IsEnabled => false;

    public Task<Result> VerifyAsync(string? token, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}
