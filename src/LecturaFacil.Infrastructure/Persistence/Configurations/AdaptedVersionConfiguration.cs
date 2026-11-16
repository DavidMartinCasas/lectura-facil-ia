// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LecturaFacil.Infrastructure.Persistence.Configurations;

/// <summary>
/// Versiones adaptadas (<c>VERSION_ADAPTADA</c> del modelo de datos), una fila por versión. Las secciones, el glosario
/// el informe de pautas y la verificación del significado se guardan como <c>jsonb</c>: no cambian y siempre se leen y escriben junto a su versión.
/// </summary>
internal static class AdaptedVersionConfiguration
{
    private const int NameMaxLength = AdaptationMetadata.NameMaxLength;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Configure(OwnedNavigationBuilder<Document, AdaptedVersion> versions)
    {
        versions.ToTable("adapted_versions");
        versions.WithOwner().HasForeignKey("DocumentId");
        versions.Property<DocumentId>("DocumentId")
            .HasColumnName("document_id")
            .HasConversion(id => id.Value, value => DocumentId.From(value));
        versions.Property(version => version.Number).HasColumnName("number").ValueGeneratedNever();
        versions.HasKey("DocumentId", nameof(AdaptedVersion.Number));

        versions.Property(version => version.Title).HasColumnName("title").IsRequired();
        versions.Property(version => version.Sections)
            .HasColumnName("sections")
            .HasColumnType("jsonb")
            .HasConversion(JsonConverter<AdaptedSection>(), JsonComparer<AdaptedSection>())
            .IsRequired();
        versions.Property(version => version.Glossary)
            .HasColumnName("glossary")
            .HasColumnType("jsonb")
            .HasConversion(JsonConverter<GlossaryTerm>(), JsonComparer<GlossaryTerm>())
            .IsRequired();
        versions.Property(version => version.GuidelineReport)
            .HasColumnName("guideline_report")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<GuidelineReport?, string>(report => SerializeReport(report!), json => DeserializeReport(json)),
                new ValueComparer<GuidelineReport?>(
                    (left, right) => ReferenceEquals(left, right)
                        || (left != null && right != null && SerializeReport(left) == SerializeReport(right)),
                    report => report == null ? 0 : SerializeReport(report).GetHashCode(StringComparison.Ordinal),
                    report => report))
            .IsRequired(false);
        versions.Property(version => version.MeaningVerification)
            .HasColumnName("meaning_verification")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<MeaningVerification?, string>(
                    verification => SerializeVerification(verification!),
                    json => DeserializeVerification(json)),
                new ValueComparer<MeaningVerification?>(
                    (left, right) => ReferenceEquals(left, right)
                        || (left != null && right != null && SerializeVerification(left) == SerializeVerification(right)),
                    verification => verification == null ? 0 : SerializeVerification(verification).GetHashCode(StringComparison.Ordinal),
                    verification => verification))
            .IsRequired(false);
        versions.Property(version => version.CreatedAt).HasColumnName("created_at");

        versions.OwnsOne(version => version.Metadata, ConfigureMetadata);
        versions.Navigation(version => version.Metadata).IsRequired();
    }

    private static void ConfigureMetadata(OwnedNavigationBuilder<AdaptedVersion, AdaptationMetadata> metadata)
    {
        metadata.Property(value => value.Provider).HasColumnName("provider").HasMaxLength(NameMaxLength).IsRequired();
        metadata.Property(value => value.Model).HasColumnName("model").HasMaxLength(NameMaxLength).IsRequired();
        metadata.Property(value => value.PromptVersion).HasColumnName("prompt_version").HasMaxLength(NameMaxLength).IsRequired();
        metadata.Property(value => value.InputTokens).HasColumnName("input_tokens");
        metadata.Property(value => value.OutputTokens).HasColumnName("output_tokens");
        metadata.Property(value => value.Duration)
            .HasColumnName("duration_ms")
            .HasConversion(duration => (long)duration.TotalMilliseconds, milliseconds => TimeSpan.FromMilliseconds(milliseconds));
    }

    private static ValueConverter<IReadOnlyList<T>, string> JsonConverter<T>() =>
        new(items => JsonSerializer.Serialize(items, Json), json => Deserialize<T>(json));

    /// <summary>Las versiones no cambian: se comparan por su JSON para que EF detecte bien los cambios.</summary>
    private static ValueComparer<IReadOnlyList<T>> JsonComparer<T>() =>
        new(
            (left, right) => JsonSerializer.Serialize(left, Json) == JsonSerializer.Serialize(right, Json),
            items => JsonSerializer.Serialize(items, Json).GetHashCode(StringComparison.Ordinal),
            items => items);

    private static IReadOnlyList<T> Deserialize<T>(string json) =>
        (JsonSerializer.Deserialize<List<T>>(json, Json) ?? []).AsReadOnly();

    private static string SerializeReport(GuidelineReport report) =>
        JsonSerializer.Serialize(report.Results.Select(StoredGuidelineResult.From), Json);

    /// <summary>
    /// Un informe guardado por otra versión de la aplicación puede traer pautas o valores que ya no existen: se descartan
    /// en lugar de impedir leer el documento.
    /// </summary>
    private static GuidelineReport DeserializeReport(string json) =>
        GuidelineReport.Restore(Deserialize<StoredGuidelineResult>(json)
            .Select(result => result.ToResult())
            .OfType<GuidelineResult>());

    private static string SerializeVerification(MeaningVerification verification) =>
        JsonSerializer.Serialize(StoredVerification.From(verification), Json);

    /// <summary>
    /// Igual que con el informe de pautas, los valores que ya no existen no impiden leer el documento. Un dato con un
    /// estado desconocido se lee como "revisar": nunca se pierde un aviso por cambiar de versión.
    /// </summary>
    private static MeaningVerification DeserializeVerification(string json)
    {
        var stored = JsonSerializer.Deserialize<StoredVerification>(json, Json);
        var status = Enum.TryParse<VerificationStatus>(stored?.Status, out var parsed) ? parsed : VerificationStatus.NotVerified;
        var facts = (stored?.KeyFacts ?? []).Select(fact => fact.ToKeyFact()).OfType<KeyFact>();
        return MeaningVerification.Restore(status, facts, stored?.Metadata);
    }

    /// <summary>Forma guardada de la verificación. Los enumerados van como texto para que el JSON sea legible.</summary>
    private sealed record StoredVerification(string Status, VerificationMetadata? Metadata, IReadOnlyList<StoredKeyFact> KeyFacts)
    {
        public static StoredVerification From(MeaningVerification verification) =>
            new(verification.Status.ToString(), verification.Metadata, [.. verification.KeyFacts.Select(StoredKeyFact.From)]);
    }

    private sealed record StoredKeyFact(string Type, string OriginalValue, string? AdaptedValue, string Status, string Reason)
    {
        public static StoredKeyFact From(KeyFact fact) =>
            new(fact.Type.ToString(), fact.OriginalValue, fact.AdaptedValue, fact.Status.ToString(), fact.Reason.ToString());

        public KeyFact? ToKeyFact()
        {
            if (string.IsNullOrWhiteSpace(OriginalValue))
            {
                return null;
            }

            var type = Enum.TryParse<KeyFactType>(Type, out var parsedType) ? parsedType : KeyFactType.Other;
            var status = Enum.TryParse<KeyFactStatus>(Status, out var parsedStatus) ? parsedStatus : KeyFactStatus.Review;
            var reason = Enum.TryParse<KeyFactReason>(Reason, out var parsedReason) ? parsedReason : KeyFactReason.NeedsReview;
            return KeyFact.Create(type, KeyFact.Truncate(OriginalValue), AdaptedValue is null ? null : KeyFact.Truncate(AdaptedValue), KeyFactVerdict.Create(status, reason));
        }
    }

    /// <summary>Forma guardada de cada pauta del informe. Los enumerados van como texto para que el JSON sea legible.</summary>
    private sealed record StoredGuidelineResult(string Code, string Status, int FindingCount, IReadOnlyList<StoredFinding> Findings)
    {
        public static StoredGuidelineResult From(GuidelineResult result) =>
            new(result.GuidelineCode, result.Status.ToString(), result.FindingCount, [.. result.Findings.Select(StoredFinding.From)]);

        public GuidelineResult? ToResult() =>
            GuidelineCatalog.All.Any(guideline => guideline.Code == Code) && Enum.TryParse<GuidelineStatus>(Status, out var status)
                ? new GuidelineResult(Code, status, FindingCount, [.. (Findings ?? []).Select(finding => finding.ToFinding(Code)).OfType<Finding>()])
                : null;
    }

    private sealed record StoredFinding(string Fragment, int Start, int End, string Severity, string Suggestion)
    {
        public static StoredFinding From(Finding finding) =>
            new(finding.Fragment, finding.Start, finding.End, finding.Severity.ToString(), finding.Suggestion);

        public Finding? ToFinding(string guidelineCode) =>
            Enum.TryParse<Severity>(Severity, out var severity)
                ? Finding.Restore(guidelineCode, Fragment, Start, End, severity, Suggestion)
                : null;
    }
}
