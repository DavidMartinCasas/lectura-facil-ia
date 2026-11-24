// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Export;

/// <summary>
/// Exporta la última versión adaptada de un documento del usuario actual (US-14). Solo se exporta cuando una persona ha
/// marcado como revisados todos los avisos de datos clave. Hasta que el grupo destinatario lo valida, sale con el sello de
/// borrador pendiente de validación; validado o publicado, con el de versión validada. Elige la
/// estrategia por formato entre los <see cref="IDocumentExporter"/> registrados: añadir un formato no cambia esta clase.
/// </summary>
public sealed class ExportDocument
{
    public const string DraftNotice = "Borrador · pendiente de validación";
    public const string ValidatedNotice = "Versión validada";
    public const string GlossaryTitle = "Glosario";
    public const string CoverSubtitle = "Adaptación a lectura fácil";
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

        // Los exportadores solo usan CPU y no reciben el token: si la petición ya se ha cancelado, no se genera nada.
        cancellationToken.ThrowIfCancellationRequested();
        var content = exporter.Export(ToExportable(latest, document));
        return Result<ExportedFile>.Success(
            new ExportedFile(ExportFileName.Create(latest.Title, exporter.FileExtension), exporter.ContentType, content));
    }

    private ExportableDocument ToExportable(AdaptedVersion version, Document document)
    {
        var today = WithLetters(_timeProvider.GetLocalNow());
        var validated = document.Status is DocumentStatus.Validated or DocumentStatus.Published;
        var notice = validated ? ValidatedNotice : DraftNotice;
        var validation = validated ? document.LatestValidation : null;
        return new ExportableDocument(
            version.Title,
            version.Sections,
            GlossaryTitle,
            version.Glossary,
            $"{notice} · {today}",
            Language,
            new ExportCover(CoverSubtitle, notice, today, validation is null ? null : ValidatedByLine(validation)));
    }

    /// <summary>"Validado por Grupo de lectura (5 personas) el 26 de noviembre de 2026".</summary>
    private string ValidatedByLine(ValidationRecord validation)
    {
        var people = validation.Participants switch
        {
            null => string.Empty,
            1 => " (1 persona)",
            var count => $" ({count} personas)",
        };
        var date = WithLetters(TimeZoneInfo.ConvertTime(validation.RecordedAt, _timeProvider.LocalTimeZone));
        return $"Validado por {validation.ValidatedBy}{people} el {date}";
    }

    private static string WithLetters(DateTimeOffset date) => date.ToString("d 'de' MMMM 'de' yyyy", Spanish);
}
