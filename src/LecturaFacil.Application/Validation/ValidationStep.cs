// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Validation;

/// <summary>
/// Lo común a cada paso de la validación: buscar el documento del usuario actual (uno ajeno no existe), comprobar que
/// el paso es posible, aplicarlo y guardarlo. Si otra pestaña ha cambiado el documento antes, se avisa en lugar de
/// pisar su cambio.
/// </summary>
public sealed class ValidationStep(
    IDocumentRepository repository,
    ICurrentUser currentUser,
    DescribeDocument describeDocument,
    TimeProvider timeProvider)
{
    /// <param name="check">Devuelve el error si el paso no es posible en el estado actual; <c>null</c> si lo es.</param>
    /// <param name="apply">Aplica el paso al documento con la hora actual.</param>
    public async Task<Result<DocumentDetails>> ExecuteAsync(
        DocumentId id,
        Func<Document, Error?> check,
        Action<Document, DateTimeOffset> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(apply);

        var document = await repository.FindForOwnerToUpdateAsync(id, currentUser.OwnerId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDetails>.Failure(DocumentErrors.NotFound);
        }

        if (check(document) is { } error)
        {
            return Result<DocumentDetails>.Failure(error);
        }

        apply(document, timeProvider.GetUtcNow());
        try
        {
            await repository.SaveAsync(document, cancellationToken);
        }
        catch (ConcurrentUpdateException)
        {
            return Result<DocumentDetails>.Failure(ReviewErrors.VersionConflict);
        }

        return Result<DocumentDetails>.Success(describeDocument.Execute(document));
    }
}
