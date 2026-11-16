// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Revisión de un borrador (US-13): <c>PUT /api/documents/{id}/versions/{n}</c> guarda una edición manual como versión
/// nueva y <c>POST /api/documents/{id}/key-facts/{factId}/acknowledge</c> marca un aviso de datos clave como revisado.
/// Ninguno llama a un modelo. Un documento ajeno responde 404, igual que uno que no existe (OWASP API1 · BOLA).
/// Solo traducen HTTP ↔ caso de uso: las reglas viven en <see cref="SaveManualEdit"/>, <see cref="AcknowledgeKeyFact"/>
/// y en Domain.
/// </summary>
internal static class DocumentReviewEndpoints
{
    private static readonly Dictionary<string, int> StatusCodesByError = new(StringComparer.Ordinal)
    {
        [DocumentErrors.NotFound.Code] = StatusCodes.Status404NotFound,
        [ReviewErrors.KeyFactNotFound.Code] = StatusCodes.Status404NotFound,
        [ReviewErrors.NotEditable.Code] = StatusCodes.Status409Conflict,
        [ReviewErrors.VersionConflict.Code] = StatusCodes.Status409Conflict,
    };

    public static RouteGroupBuilder MapDocumentReview(this RouteGroupBuilder group)
    {
        // TODO(US-15): RequireAuthorization() y antiforgery cuando haya usuarios y cookies de sesión. Sin cookies no hay
        // credenciales que un sitio ajeno pueda aprovechar (CSRF); con ellas, estas peticiones cambian estado y la necesitan.
        group.MapPut("/{id:guid}/versions/{version:int}", SaveEditAsync)
            .Accepts<ManualEditRequest>("application/json")
            .Produces<DocumentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("SaveManualEdit")
            .WithSummary("Guarda la edición manual de la versión {version} (que debe ser la última) como una versión nueva.")
            .WithDescription(
                "Recalcula las pautas, los datos clave (con código) y la legibilidad sin llamar a ningún modelo. "
                + "Devuelve el documento con la versión nueva. 409 si el documento no está en borrador o ya hay otra versión.");

        group.MapPost("/{id:guid}/key-facts/{factId:int}/acknowledge", AcknowledgeAsync)
            .Accepts<AcknowledgeKeyFactRequest>("application/json")
            .Produces<DocumentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("AcknowledgeKeyFact")
            .WithSummary("Marca como revisado un aviso de los datos clave de la última versión.")
            .WithDescription("factId es el id del dato en la respuesta del documento. El aviso se sigue mostrando.");

        return group;
    }

    private static async Task<IResult> SaveEditAsync(
        Guid id,
        int version,
        ManualEditRequest request,
        IValidator<ManualEditRequest> validator,
        SaveManualEdit saveManualEdit,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ToProblem(DocumentErrors.NotFound);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var command = new SaveManualEditCommand(
            DocumentId.From(id),
            version,
            request.Title!,
            [.. request.Sections!.Select(section => new AdaptedSectionDraft(section!.Question!, [.. section.Sentences!.Select(sentence => sentence!)]))],
            [.. (request.Glossary ?? []).Select(term => new GlossaryTermDraft(term!.Term!, term.Definition!))]);
        return ToResponse(await saveManualEdit.ExecuteAsync(command, cancellationToken));
    }

    private static async Task<IResult> AcknowledgeAsync(
        Guid id,
        int factId,
        AcknowledgeKeyFactRequest request,
        IValidator<AcknowledgeKeyFactRequest> validator,
        AcknowledgeKeyFact acknowledgeKeyFact,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ToProblem(DocumentErrors.NotFound);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var command = new AcknowledgeKeyFactCommand(DocumentId.From(id), request.Version!.Value, factId);
        return ToResponse(await acknowledgeKeyFact.ExecuteAsync(command, cancellationToken));
    }

    private static IResult ToResponse(Result<DocumentDetails> result) =>
        result.IsSuccess ? TypedResults.Ok(DocumentResponse.From(result.Value)) : ToProblem(result.Error!);

    private static IResult ToProblem(Error error) =>
        TypedResults.Problem(
            statusCode: StatusCodesByError.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest),
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
