// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Export;

/// <summary>
/// Exporta la última versión adaptada de un documento del usuario actual (US-14). Solo se exporta cuando una persona ha
/// marcado como revisados todos los avisos de datos clave, y siempre como borrador pendiente de validación. Elige la
/// estrategia por formato entre los <see cref="IDocumentExporter"/> registrados: añadir un formato no cambia esta clase.
/// </summary>
public sealed class ExportDocument
{
    public const string DraftNotice = "Borrador · pendiente de validación";
    public const string GlossaryTitle = "Glosario";
    public const string Language = "es";

    // La fecha escrita con letras, como piden las pautas de lectura fácil, y en una cultura fija.
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    private readonly IDocumentRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<ExportFormat, IDocumentExporter> _exporters;

    public ExportDocument(
        IDocumentRepository repository,
        ICurrentUser currentUser,
        IEnumerable<IDocumentExporter> exporters,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(exporters);

        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;

        // ToDictionary lanza si dos estrategias dicen ser del mismo formato: es un error de composición.
        _exporters = exporters.ToDictionary(exporter => exporter.Format);
    }

    public async Task<Result<ExportedFile>> ExecuteAsync(DocumentId id, string format, CancellationToken cancellationToken)
    {
        if (!ExportFormats.TryParse(format, out var exportFormat) || !_exporters.TryGetValue(exportFormat, out var exporter))
        {
            return Result<ExportedFile>.Failure(ExportErrors.UnsupportedFormat);
        }

        var document = await _repository.FindForOwnerAsync(id, _currentUser.OwnerId, cancellationToken);
        if (document is null)
        {
            return Result<ExportedFile>.Failure(DocumentErrors.NotFound);
        }

        var latest = document.LatestAdaptedVersion;
        if (latest is null)
        {
            return Result<ExportedFile>.Failure(ExportErrors.NothingToExport);
        }

        if (!latest.CanExport)
        {
            return Result<ExportedFile>.Failure(ExportErrors.PendingWarnings);
        }

        var content = exporter.Export(ToExportable(latest));
        return Result<ExportedFile>.Success(
            new ExportedFile(ExportFileName.Create(latest.Title, exporter.FileExtension), exporter.ContentType, content));
    }

    private ExportableDocument ToExportable(AdaptedVersion version) =>
        new(version.Title, version.Sections, GlossaryTitle, version.Glossary, Footer(), Language);

    private string Footer()
    {
        var today = _timeProvider.GetLocalNow();
        return $"{DraftNotice} · {today.ToString("d 'de' MMMM 'de' yyyy", Spanish)}";
    }
}
