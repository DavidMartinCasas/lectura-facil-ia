// SPDX-License-Identifier: Apache-2.0
using System.Runtime.CompilerServices;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Documents;

/// <summary>
/// Sigue el estado de un documento del usuario actual: envía el estado actual y cada cambio mientras se adapta.
/// Termina cuando la adaptación acaba (bien o mal), al pasar el tiempo máximo o cuando el cliente se desconecta.
/// </summary>
public sealed class WatchDocument(
    IDocumentRepository repository,
    ICurrentUser currentUser,
    IOptions<LimitsOptions> limits,
    TimeProvider timeProvider)
{
    private readonly LimitsOptions _limits = limits.Value;

    /// <returns>Los estados, o <see cref="DocumentErrors.NotFound"/> si el documento no existe o no es del usuario.</returns>
    public async Task<Result<IAsyncEnumerable<DocumentStatus>>> StartAsync(DocumentId id, CancellationToken cancellationToken)
    {
        var owner = currentUser.OwnerId;
        var status = await repository.FindStatusForOwnerAsync(id, owner, cancellationToken);
        return status is { } current
            ? Result<IAsyncEnumerable<DocumentStatus>>.Success(Follow(id, owner, current, cancellationToken))
            : Result<IAsyncEnumerable<DocumentStatus>>.Failure(DocumentErrors.NotFound);
    }

    private async IAsyncEnumerable<DocumentStatus> Follow(
        DocumentId id,
        OwnerId owner,
        DocumentStatus current,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return current;

        var interval = TimeSpan.FromMilliseconds(_limits.DocumentEventsPollMilliseconds);
        var deadline = timeProvider.GetUtcNow().AddSeconds(_limits.DocumentEventsMaxSeconds);
        while (DocumentStatusTransitions.IsAwaitingAdaptation(current) && timeProvider.GetUtcNow() < deadline)
        {
            await Task.Delay(interval, timeProvider, cancellationToken);
            if (await repository.FindStatusForOwnerAsync(id, owner, cancellationToken) is not { } next)
            {
                yield break;
            }

            if (next != current)
            {
                current = next;
                yield return current;
            }
        }
    }
}
