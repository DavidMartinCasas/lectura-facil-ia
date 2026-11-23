// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Vuelve a adaptar un documento del usuario actual cuya adaptación ha fallado: lo deja en "Procesando" y lo pone en la
/// cola, igual que al enviarlo. Solo si el fallo se puede arreglar reintentando (<see cref="AdaptationErrors.IsRetriable"/>).
/// Cada reintento llama al modelo: la API le aplica el mismo límite por usuario que al envío.
/// </summary>
public sealed class RetryAdaptation(
    IDocumentRepository repository,
    ICurrentUser currentUser,
    IAdaptationQueue adaptationQueue,
    DescribeDocument describeDocument,
    TimeProvider timeProvider)
{
    public async Task<Result<DocumentDetails>> ExecuteAsync(DocumentId id, CancellationToken cancellationToken)
    {
        var document = await repository.FindForOwnerToUpdateAsync(id, currentUser.OwnerId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDetails>.Failure(DocumentErrors.NotFound);
        }

        if (document.Status != DocumentStatus.Error)
        {
            return Result<DocumentDetails>.Failure(AdaptationErrors.NotFailed);
        }

        if (!AdaptationErrors.IsRetriable(document.FailureCode))
        {
            return Result<DocumentDetails>.Failure(AdaptationErrors.NotRetriable);
        }

        document.RetryAdaptation(timeProvider.GetUtcNow());
        try
        {
            await repository.SaveAsync(document, cancellationToken);
        }
        catch (ConcurrentUpdateException)
        {
            // Otra pestaña ya lo ha reintentado: el documento ya no está con error.
            return Result<DocumentDetails>.Failure(AdaptationErrors.NotFailed);
        }

        await adaptationQueue.EnqueueAsync(document.Id, cancellationToken);
        return Result<DocumentDetails>.Success(describeDocument.Execute(document));
    }
}
