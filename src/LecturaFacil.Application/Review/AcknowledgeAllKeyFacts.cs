// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Users;

namespace LecturaFacil.Application.Review;

/// <summary>
/// Una persona marca de una vez como revisados todos los avisos de los datos clave de la última versión (US-13), después
/// de comprobarlos en el original. Se marcan todos o ninguno: es un solo guardado. Los avisos siguen a la vista con su
/// motivo; solo dejan de impedir la exportación. Sin avisos pendientes no guarda nada.
/// </summary>
public sealed class AcknowledgeAllKeyFacts(
    IDocumentRepository repository,
    ICurrentUser currentUser,
    DescribeDocument describeDocument,
    TimeProvider timeProvider)
{
    public async Task<Result<DocumentDetails>> ExecuteAsync(AcknowledgeAllKeyFactsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = await repository.FindForOwnerToUpdateAsync(command.DocumentId, currentUser.OwnerId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDetails>.Failure(DocumentErrors.NotFound);
        }

        var latest = ReviewGuard.LatestEditableVersion(document, command.Version);
        if (latest.IsFailure)
        {
            return Result<DocumentDetails>.Failure(latest.Error!);
        }

        if (latest.Value.PendingKeyFactCount == 0)
        {
            return Result<DocumentDetails>.Success(describeDocument.Execute(document));
        }

        document.AcknowledgeAllKeyFacts(timeProvider.GetUtcNow());
        try
        {
            await repository.SaveAsync(document, cancellationToken);
        }
        catch (ConcurrentUpdateException)
        {
            // Otra pestaña o persona ha guardado antes: se avisa en lugar de pisar su cambio.
            return Result<DocumentDetails>.Failure(ReviewErrors.VersionConflict);
        }

        return Result<DocumentDetails>.Success(describeDocument.Execute(document));
    }
}
