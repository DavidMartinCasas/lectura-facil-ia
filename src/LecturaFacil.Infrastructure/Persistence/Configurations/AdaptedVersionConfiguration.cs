// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LecturaFacil.Infrastructure.Persistence.Configurations;

/// <summary>
/// Versiones adaptadas (<c>VERSION_ADAPTADA</c> del modelo de datos), una fila por versión. Las secciones, el glosario
/// y el informe de pautas se guardan como <c>jsonb</c>: no cambian y siempre se leen y escriben junto a su versión.
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

    private static GuidelineReport DeserializeReport(string json) =>
        GuidelineReport.Restore(Deserialize<StoredGuidelineResult>(json).Select(result => result.ToResult()));

    /// <summary>Forma guardada de cada pauta del informe. Los enumerados van como texto para que el JSON sea legible.</summary>
    private sealed record StoredGuidelineResult(string Code, string Status, int FindingCount, IReadOnlyList<StoredFinding> Findings)
    {
        public static StoredGuidelineResult From(GuidelineResult result) =>
            new(result.GuidelineCode, result.Status.ToString(), result.FindingCount, [.. result.Findings.Select(StoredFinding.From)]);

        public GuidelineResult ToResult() =>
            new(Code, Enum.Parse<GuidelineStatus>(Status), FindingCount, [.. Findings.Select(finding => finding.ToFinding(Code))]);
    }

    private sealed record StoredFinding(string Fragment, int Start, int End, string Severity, string Suggestion)
    {
        public static StoredFinding From(Finding finding) =>
            new(finding.Fragment, finding.Start, finding.End, finding.Severity.ToString(), finding.Suggestion);

        public Finding ToFinding(string guidelineCode) =>
            Finding.Restore(guidelineCode, Fragment, Start, End, Enum.Parse<Severity>(Severity), Suggestion);
    }
}
