// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Readability;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Readability;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Documents;

/// <summary>
/// Documentos del usuario actual, del más nuevo al más antiguo, por páginas y con filtro por título y estado.
/// Cada página tiene como mucho <see cref="LimitsOptions.MaxDocumentsPerList"/> documentos. La legibilidad de cada
/// documento se calcula al leer, igual que en su ficha: es cálculo en memoria y de coste lineal.
/// </summary>
public sealed class ListDocuments(
    IDocumentRepository repository,
    ICurrentUser currentUser,
    IOptions<LimitsOptions> limits,
    CompareReadability compareReadability)
{
    /// <summary>Documentos por página si el cliente no dice otra cosa.</summary>
    public const int DefaultPageSize = 20;

    private static readonly string[] StatusNames = Enum.GetNames<DocumentStatus>();

    private readonly int _maxPageSize = limits.Value.MaxDocumentsPerList;

    public async Task<Result<DocumentPage>> ExecuteAsync(DocumentListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = Parse(request);
        if (query.IsFailure)
        {
            return Result<DocumentPage>.Failure(query.Error!);
        }

        var page = await repository.ListForOwnerAsync(currentUser.OwnerId, query.Value, cancellationToken);
        return Result<DocumentPage>.Success(page with { Items = [.. page.Items.Select(WithReadability)] });
    }

    /// <summary>Calcula la legibilidad con los textos que trae el resumen y los quita: no hacen falta fuera de aquí.</summary>
    private DocumentSummary WithReadability(DocumentSummary summary)
    {
        if (summary.ReadabilityTexts is not { } texts)
        {
            return summary;
        }

        var readability = compareReadability.Compare(texts.Original, texts.Adapted);
        return summary with
        {
            ReadabilityTexts = null,
            Readability = readability.IsSuccess ? readability.Value : null,
        };
    }

    private Result<DocumentListQuery> Parse(DocumentListRequest request)
    {
        var pageSize = request.PageSize ?? Math.Min(DefaultPageSize, _maxPageSize);
        if (pageSize < 1 || pageSize > _maxPageSize)
        {
            return Result<DocumentListQuery>.Failure(DocumentListErrors.InvalidPageSize(_maxPageSize));
        }

        var title = NormalizeTitle(request.Title);
        if (title?.Length > DocumentTitle.MaxLength)
        {
            return Result<DocumentListQuery>.Failure(DocumentListErrors.SearchTooLong);
        }

        var statuses = new HashSet<DocumentStatus>();
        foreach (var value in request.Statuses ?? [])
        {
            // Solo nombres: Enum.TryParse también aceptaría números ("3") o listas ("Draft,Error").
            if (!StatusNames.Contains(value, StringComparer.Ordinal))
            {
                return Result<DocumentListQuery>.Failure(DocumentListErrors.InvalidStatus);
            }

            statuses.Add(Enum.Parse<DocumentStatus>(value));
        }

        DocumentListCursor? after = null;
        if (request.Cursor is not null)
        {
            if (!DocumentListCursor.TryDecode(request.Cursor, out var cursor))
            {
                return Result<DocumentListQuery>.Failure(DocumentListErrors.InvalidCursor);
            }

            after = cursor;
        }

        return Result<DocumentListQuery>.Success(new DocumentListQuery(title, statuses, after, pageSize));
    }

    /// <summary>Quita los espacios sobrantes y los caracteres de control. Vacío = sin filtro.</summary>
    private static string? NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var withoutControls = new string([.. value.Select(character => char.IsControl(character) ? ' ' : character)]);
        var normalized = string.Join(' ', withoutControls.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Normalize(NormalizationForm.FormC);
        return normalized.Length == 0 ? null : normalized;
    }
}

/// <summary>Lo que pide el cliente, tal cual llega: <see cref="ListDocuments"/> lo valida.</summary>
/// <param name="Title">Texto a buscar en el título.</param>
/// <param name="Statuses">Nombres de estado (<see cref="DocumentStatus"/>); vacío = todos.</param>
/// <param name="Cursor">Cursor de la página anterior (<see cref="DocumentPage.Next"/>).</param>
/// <param name="PageSize">Documentos por página; <c>null</c> = <see cref="ListDocuments.DefaultPageSize"/>.</param>
public sealed record DocumentListRequest(
    string? Title = null,
    IReadOnlyList<string>? Statuses = null,
    string? Cursor = null,
    int? PageSize = null);

/// <summary>Errores al pedir la lista. Los mensajes se muestran al usuario.</summary>
public static class DocumentListErrors
{
    public static readonly Error InvalidCursor = new(
        "documents.invalid_cursor",
        "No se ha podido cargar la página siguiente. Vuelve a cargar la lista.");

    public static readonly Error InvalidStatus = new(
        "documents.invalid_status",
        "El estado elegido no existe.");

    public static readonly Error SearchTooLong = new(
        "documents.search_too_long",
        string.Create(CultureInfo.InvariantCulture, $"El texto de búsqueda no puede superar {DocumentTitle.MaxLength} caracteres."));

    public static Error InvalidPageSize(int maxPageSize) => new(
        "documents.invalid_page_size",
        string.Create(CultureInfo.InvariantCulture, $"Se pueden pedir entre 1 y {maxPageSize} documentos por página."));
}

/// <summary>Un documento en la lista "Mis documentos".</summary>
/// <param name="Provider">Nombre visible del proveedor que generó la última adaptación; <c>null</c> si aún no hay ninguna.</param>
/// <param name="PendingKeyFactCount">
/// Datos clave de la última versión sin marcar como revisados: lo que bloquea la exportación. <c>null</c> si aún no hay
/// ninguna versión.
/// </param>
/// <param name="BrokenGuidelineCount">
/// Pautas que la última versión no cumple. Son orientativas. <c>null</c> si aún no hay ninguna versión.
/// </param>
/// <param name="ReadabilityTexts">
/// El original y la última versión, para calcular la legibilidad. <see cref="ListDocuments"/> la calcula y los quita.
/// </param>
/// <param name="Readability">
/// Legibilidad del original y de la última versión, la misma que en la ficha. <c>null</c> si no hay versión o no se puede
/// calcular.
/// </param>
public sealed record DocumentSummary(
    DocumentId Id,
    string Title,
    DocumentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Provider,
    int? PendingKeyFactCount = null,
    int? BrokenGuidelineCount = null,
    ReadabilityTexts? ReadabilityTexts = null,
    ReadabilityComparison? Readability = null)
{
    public static DocumentSummary From(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Create(
            document.Id,
            document.Title.Value,
            document.Status,
            document.CreatedAt,
            document.UpdatedAt,
            document.OriginalText.Value,
            document.LatestAdaptedVersion);
    }

    /// <summary>El resumen de un documento a partir de su texto original y su última versión (<c>null</c> si no tiene).</summary>
    public static DocumentSummary Create(
        DocumentId id,
        string title,
        DocumentStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string originalText,
        AdaptedVersion? latest) =>
        new(
            id,
            title,
            status,
            createdAt,
            updatedAt,
            latest?.Metadata.Provider,
            latest?.PendingKeyFactCount,
            latest?.BrokenGuidelineCount,
            latest is null ? null : new ReadabilityTexts(originalText, latest.PlainText));
}

/// <summary>Los dos textos que compara la legibilidad: el original y el de la última versión.</summary>
public sealed record ReadabilityTexts(string Original, string Adapted);
