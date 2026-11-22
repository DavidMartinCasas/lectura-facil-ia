// SPDX-License-Identifier: Apache-2.0
using System.Runtime.CompilerServices;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Documents;

/// <summary>
/// Sigue el estado de un documento del usuario actual: envía el estado actual y cada cambio mientras se adapta, también
/// cada cambio de fase (adaptando, revisando pautas, verificando) para enseñar el progreso real.
/// Termina cuando la adaptación acaba (bien o mal), al pasar el tiempo máximo o cuando el cliente se desconecta.
/// </summary>
public sealed class WatchDocument(
    IDocumentRepository repository,
    ICurrentUser currentUser,
    IOptions<LimitsOptions> limits,
    TimeProvider timeProvider)
{
    private readonly LimitsOptions _limits = limits.Value;

    /// <returns>El progreso, o <see cref="DocumentErrors.NotFound"/> si el documento no existe o no es del usuario.</returns>
    public async Task<Result<IAsyncEnumerable<DocumentProgress>>> StartAsync(DocumentId id, CancellationToken cancellationToken)
    {
        var owner = currentUser.OwnerId;
        var progress = await repository.FindProgressForOwnerAsync(id, owner, cancellationToken);
        return progress is { } current
            ? Result<IAsyncEnumerable<DocumentProgress>>.Success(Follow(id, owner, current, cancellationToken))
            : Result<IAsyncEnumerable<DocumentProgress>>.Failure(DocumentErrors.NotFound);
    }

    private async IAsyncEnumerable<DocumentProgress> Follow(
        DocumentId id,
        OwnerId owner,
        DocumentProgress current,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return current;

        var interval = TimeSpan.FromMilliseconds(_limits.DocumentEventsPollMilliseconds);
        var deadline = timeProvider.GetUtcNow().AddSeconds(_limits.DocumentEventsMaxSeconds);
        while (DocumentStatusTransitions.IsAwaitingAdaptation(current.Status) && timeProvider.GetUtcNow() < deadline)
        {
            await Task.Delay(interval, timeProvider, cancellationToken);
            if (await repository.FindProgressForOwnerAsync(id, owner, cancellationToken) is not { } next)
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
