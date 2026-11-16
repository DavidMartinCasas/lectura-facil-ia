// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Users;
using LecturaFacil.Application.Verification;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Verification;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Review;

/// <summary>
/// Guarda una edición manual de la adaptación como una versión nueva (US-13). Vuelve a revisar las pautas
/// (<see cref="ReviewAdaptation"/>) y a comprobar con código los datos clave
/// (<see cref="MeaningVerification.RecheckAfterEdit"/>) sin llamar a ningún modelo: una edición nunca envía el documento
/// fuera. La legibilidad se recalcula al leer el documento (<see cref="DescribeDocument"/>).
/// </summary>
public sealed class SaveManualEdit(
    IDocumentRepository repository,
    ICurrentUser currentUser,
    ReviewAdaptation reviewAdaptation,
    DescribeDocument describeDocument,
    IOptions<LimitsOptions> limits,
    TimeProvider timeProvider)
{
    /// <summary>Lo que se guarda como versión del prompt de una versión editada a mano: ningún prompt la ha generado.</summary>
    public const string ManualEditPromptVersion = "manual";

    private readonly LimitsOptions _limits = limits.Value;

    public async Task<Result<DocumentDetails>> ExecuteAsync(SaveManualEditCommand command, CancellationToken cancellationToken)
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

        var content = AdaptedContent.TryCreate(command.Title, command.Sections, command.Glossary);
        if (content is null)
        {
            return Result<DocumentDetails>.Failure(ReviewErrors.IncompleteEdit);
        }

        var adaptedText = VerifyMeaning.TextToVerify(content);
        if (adaptedText.Length > _limits.MaxEditedCharacters)
        {
            return Result<DocumentDetails>.Failure(ReviewErrors.EditTooLong(_limits.MaxEditedCharacters));
        }

        var report = reviewAdaptation.Execute(content);
        var verification = Recheck(document.OriginalText.Value, latest.Value.MeaningVerification, adaptedText);
        var metadata = latest.Value.Metadata with
        {
            PromptVersion = ManualEditPromptVersion,
            InputTokens = 0,
            OutputTokens = 0,
            Duration = TimeSpan.Zero,
        };
        document.SaveManualEdit(content, metadata, report, verification, timeProvider.GetUtcNow());
        await repository.SaveAsync(document, cancellationToken);
        return Result<DocumentDetails>.Success(describeDocument.Execute(document));
    }

    /// <summary>
    /// Los datos clave de la versión anterior, comprobados otra vez con código sobre el texto editado. Sin verificación
    /// previa, o si el texto es anómalo, se pide revisar las cifras o el documento entero (fallo seguro).
    /// </summary>
    private MeaningVerification Recheck(string originalText, MeaningVerification? previous, string adaptedText)
    {
        try
        {
            return previous is null
                ? MeaningVerification.NotVerified(originalText, _limits.MaxKeyFactsPerVersion)
                : previous.RecheckAfterEdit(originalText, adaptedText, _limits.MaxKeyFactsPerVersion);
        }
        catch (RegexMatchTimeoutException)
        {
            return MeaningVerification.Unreadable();
        }
    }
}
