// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// <c>POST /api/documents</c>: texto pegado (JSON) o PDF (<c>multipart/form-data</c>, campo <c>file</c>). El documento
/// queda en la cola de adaptación. Las consultas están en <see cref="DocumentQueryEndpoints"/>.
/// Solo traduce HTTP ↔ caso de uso: las reglas viven en <see cref="SubmitDocument"/> y en Domain.
/// </summary>
internal static partial class DocumentEndpoints
{
    public const string Path = "/api/documents";

    /// <summary>Cada documento recibido se adapta con el modelo: el envío tiene su propio límite de peticiones.</summary>
    public const string SubmitRateLimitPolicy = "document-submit";

    private const string JsonContentType = "application/json";
    private const string MultipartContentType = "multipart/form-data";

    private static readonly Dictionary<string, int> StatusCodesByError = new(StringComparer.Ordinal)
    {
        [DocumentErrors.FileTooLargeCode] = StatusCodes.Status413PayloadTooLarge,
        [DocumentErrors.InvalidFileType.Code] = StatusCodes.Status400BadRequest,
        [DocumentErrors.TextTooShortCode] = StatusCodes.Status400BadRequest,
        [DocumentErrors.TextTooLongCode] = StatusCodes.Status400BadRequest,
        [DocumentErrors.ScannedPdf.Code] = StatusCodes.Status422UnprocessableEntity,
        [DocumentErrors.UnreadablePdf.Code] = StatusCodes.Status422UnprocessableEntity,
        [DocumentErrors.PdfTooComplex.Code] = StatusCodes.Status422UnprocessableEntity,
        [DocumentErrors.Busy.Code] = StatusCodes.Status503ServiceUnavailable,
    };

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // TODO(US-15): RequireAuthorization() y antiforgery cuando haya usuarios y cookies. El formulario se lee a mano,
        // así que Minimal APIs no añade la validación sola: hace falta .WithMetadata(new RequireAntiforgeryTokenAttribute()).
        var group = endpoints.MapGroup(Path).WithTags("Documentos");
        group.MapDocumentQueries();
        group.MapDocumentReview();

        group.MapPost("/", SubmitAsync)
            .RequireRateLimiting(SubmitRateLimitPolicy)
            .Accepts<SubmitTextRequest>(JsonContentType, MultipartContentType)
            .AddOpenApiOperationTransformer(DescribeMultipartBody)
            .Produces<SubmitDocumentResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("SubmitDocument")
            .WithSummary("Sube un texto pegado o un PDF digital y crea un documento en estado Received.")
            .WithDescription(
                "JSON { text, title? } con el texto pegado, o multipart/form-data con el PDF en el campo 'file'. "
                + "El PDF se procesa en memoria y solo se guarda el texto extraído.");

        return endpoints;
    }

    /// <summary>En OpenAPI, el cuerpo multipart es el campo <c>file</c> con el PDF, no el JSON del texto.</summary>
    private static Task DescribeMultipartBody(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.RequestBody?.Content?.TryGetValue(MultipartContentType, out var multipart) == true)
        {
            multipart.Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string> { SubmitPdfForm.FieldName },
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    [SubmitPdfForm.FieldName] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                },
            };
        }

        return Task.CompletedTask;
    }

    private static async Task<IResult> SubmitAsync(
        HttpRequest request,
        SubmitDocument submitDocument,
        IValidator<SubmitTextRequest> textValidator,
        IValidator<SubmitPdfForm> pdfValidator,
        IOptions<LimitsOptions> limits,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > RequestLimits.MaxRequestBytes(limits.Value))
        {
            return ToProblem(DocumentErrors.FileTooLarge(limits.Value.MaxFileSizeBytes));
        }

        try
        {
            var result = request.HasFormContentType
                ? await SubmitPdfAsync(request, submitDocument, pdfValidator, cancellationToken)
                : request.HasJsonContentType()
                    ? await SubmitTextAsync(request, submitDocument, textValidator, cancellationToken)
                    : TypedResults.Problem(
                        statusCode: StatusCodes.Status415UnsupportedMediaType,
                        detail: "Envía el texto como JSON o el PDF como multipart/form-data.");
            Log(loggerFactory, result);
            return result.Result;
        }
        catch (BadHttpRequestException exception)
        {
            // Kestrel corta las peticiones que superan el tamaño máximo (413) o están mal formadas (400).
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? ToProblem(DocumentErrors.FileTooLarge(limits.Value.MaxFileSizeBytes))
                : TypedResults.Problem(statusCode: exception.StatusCode);
        }
        catch (InvalidDataException)
        {
            // Formulario multipart mal formado o por encima de los límites de FormOptions.
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "La petición no es válida.");
        }
    }

    private static async Task<SubmitOutcome> SubmitTextAsync(
        HttpRequest request,
        SubmitDocument submitDocument,
        IValidator<SubmitTextRequest> validator,
        CancellationToken cancellationToken)
    {
        SubmitTextRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<SubmitTextRequest>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: "El JSON no es válido.");
        }

        body ??= new SubmitTextRequest(null, null);
        var validation = await validator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        return await submitDocument.SubmitTextAsync(new SubmitTextCommand(body.Text!, body.Title), cancellationToken);
    }

    private static async Task<SubmitOutcome> SubmitPdfAsync(
        HttpRequest request,
        SubmitDocument submitDocument,
        IValidator<SubmitPdfForm> validator,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var upload = new SubmitPdfForm(form.Files.GetFile(SubmitPdfForm.FieldName));
        var validation = await validator.ValidateAsync(upload, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        await using var content = upload.File!.OpenReadStream();
        return await submitDocument.SubmitPdfAsync(new SubmitPdfCommand(content, upload.File.FileName), cancellationToken);
    }

    private static IResult ToProblem(Error error) =>
        TypedResults.Problem(
            statusCode: StatusCodesByError.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest),
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    private static void Log(ILoggerFactory loggerFactory, SubmitOutcome outcome)
    {
        if (outcome.Submitted is not { } submitted)
        {
            return;
        }

        // Nunca el texto ni el nombre del fichero: solo identificador y tamaños.
        DocumentReceived(
            loggerFactory.CreateLogger(typeof(DocumentEndpoints).FullName!),
            submitted.Id.Value,
            submitted.Characters,
            submitted.PageCount ?? 0);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Documento {DocumentId} recibido: {Characters} caracteres, {Pages} páginas.")]
    private static partial void DocumentReceived(ILogger logger, Guid documentId, int characters, int pages);

    /// <summary>Respuesta HTTP y, si se creó, el documento (para registrarlo).</summary>
    private sealed record SubmitOutcome(IResult Result, SubmittedDocument? Submitted)
    {
        public static implicit operator SubmitOutcome(Result<SubmittedDocument> result) => result.IsSuccess
            ? new SubmitOutcome(
                TypedResults.Accepted((string?)null, new SubmitDocumentResponse(result.Value.Id.Value, result.Value.Status.ToString())),
                result.Value)
            : new SubmitOutcome(ToProblem(result.Error!), null);

        public static implicit operator SubmitOutcome(ProblemHttpResult problem) => new(problem, null);

        public static implicit operator SubmitOutcome(ValidationProblem problem) => new(problem, null);
    }
}
