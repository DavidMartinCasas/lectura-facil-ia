// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Users;

namespace LecturaFacil.Application.Review;

/// <summary>
/// Una persona marca como revisado un aviso de los datos clave de la última versión (US-13). El aviso sigue a la vista
/// con su motivo; solo deja de impedir la exportación.
/// </summary>
public sealed class AcknowledgeKeyFact(
    IDocumentRepository repository,
    ICurrentUser currentUser,
    DescribeDocument describeDocument,
    TimeProvider timeProvider)
{
    public async Task<Result<DocumentDetails>> ExecuteAsync(AcknowledgeKeyFactCommand command, CancellationToken cancellationToken)
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

        var keyFactCount = latest.Value.MeaningVerification?.KeyFacts.Count ?? 0;
        if (command.KeyFactIndex < 0 || command.KeyFactIndex >= keyFactCount)
        {
            return Result<DocumentDetails>.Failure(ReviewErrors.KeyFactNotFound);
        }

        document.AcknowledgeKeyFact(command.KeyFactIndex, timeProvider.GetUtcNow());
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
