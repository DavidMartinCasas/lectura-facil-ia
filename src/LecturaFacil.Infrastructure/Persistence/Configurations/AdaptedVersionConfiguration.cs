// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using LecturaFacil.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LecturaFacil.Infrastructure.Persistence.Configurations;

/// <summary>
/// Versiones adaptadas (<c>VERSION_ADAPTADA</c> del modelo de datos), una fila por versión. Las secciones y el glosario
/// se guardan como <c>jsonb</c>: son texto plano estructurado que siempre se lee y escribe junto a su versión.
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
}
