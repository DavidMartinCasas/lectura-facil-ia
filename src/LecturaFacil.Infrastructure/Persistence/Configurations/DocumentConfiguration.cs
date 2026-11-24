// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LecturaFacil.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    private const int StatusMaxLength = 32;

    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => DocumentId.From(value))
            .ValueGeneratedNever();

        builder.Property(document => document.OwnerId)
            .HasColumnName("owner_id")
            .HasConversion(owner => owner.Value, value => OwnerId.From(value))
            .IsRequired();

        builder.Property(document => document.Title)
            .HasColumnName("title")
            .HasConversion(title => title.Value, value => DocumentTitle.Restore(value))
            .HasMaxLength(DocumentTitle.MaxLength)
            .IsRequired();

        builder.Property(document => document.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(StatusMaxLength)
            .IsRequired();

        builder.Property(document => document.OriginalText)
            .HasColumnName("original_text")
            .HasConversion(text => text.Value, value => OriginalText.Restore(value))
            .IsRequired();

        builder.Property(document => document.Stage)
            .HasColumnName("processing_stage")
            .HasConversion<string>()
            .HasMaxLength(StatusMaxLength);

        builder.Property(document => document.FailureCode)
            .HasColumnName("failure_code")
            .HasMaxLength(Document.FailureCodeMaxLength);

        builder.Property(document => document.CreatedAt).HasColumnName("created_at");
        // Concurrencia optimista: cada cambio del documento cambia UpdatedAt, así dos peticiones que lo modifican a la vez
        // (dos ediciones, o dos datos clave marcados como revisados) no se pisan; la segunda falla (US-13).
        builder.Property(document => document.UpdatedAt).HasColumnName("updated_at").IsConcurrencyToken();

        // Todas las consultas de documentos filtran por propietario (autorización por objeto).
        builder.HasIndex(document => new { document.OwnerId, document.CreatedAt });

        // El worker busca al arrancar los documentos que esperan su adaptación.
        builder.HasIndex(document => document.Status);

        builder.OwnsMany(document => document.OriginalFindings, ConfigureFindings);
        builder.Navigation(document => document.OriginalFindings).HasField("_originalFindings");

        builder.OwnsMany(document => document.AdaptedVersions, AdaptedVersionConfiguration.Configure);
        builder.Navigation(document => document.AdaptedVersions).HasField("_adaptedVersions");

        builder.OwnsMany(document => document.Validations, ConfigureValidations);
        builder.Navigation(document => document.Validations).HasField("_validations");
    }

    /// <summary>Validaciones anotadas (<c>VALIDACION</c> del modelo de datos), una fila por ronda. Se borran con su documento.</summary>
    private static void ConfigureValidations(OwnedNavigationBuilder<Document, ValidationRecord> validations)
    {
        validations.ToTable("validations");
        validations.WithOwner().HasForeignKey("DocumentId");
        validations.Property<DocumentId>("DocumentId")
            .HasColumnName("document_id")
            .HasConversion(id => id.Value, value => DocumentId.From(value));
        validations.Property(validation => validation.Number).HasColumnName("number").ValueGeneratedNever();
        validations.HasKey("DocumentId", nameof(ValidationRecord.Number));

        validations.Property(validation => validation.VersionNumber).HasColumnName("version_number");
        validations.Property(validation => validation.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(StatusMaxLength)
            .IsRequired();
        validations.Property(validation => validation.ValidatedBy)
            .HasColumnName("validated_by")
            .HasMaxLength(ValidationRecord.ValidatedByMaxLength)
            .IsRequired();
        validations.Property(validation => validation.Notes)
            .HasColumnName("notes")
            .HasMaxLength(ValidationRecord.NotesMaxLength);
        validations.Property(validation => validation.Participants).HasColumnName("participants");
        validations.Property(validation => validation.HardSections)
            .HasColumnName("hard_sections")
            .HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<IReadOnlyList<string>, string>(
                    questions => JsonSerializer.Serialize(questions, JsonSerializerOptions.Default),
                    json => (JsonSerializer.Deserialize<List<string>>(json, JsonSerializerOptions.Default) ?? new List<string>()).AsReadOnly()),
                new ValueComparer<IReadOnlyList<string>>(
                    (left, right) => left!.SequenceEqual(right!),
                    questions => questions.Aggregate(0, (hash, question) => HashCode.Combine(hash, question.GetHashCode(StringComparison.Ordinal))),
                    questions => questions.ToList().AsReadOnly()))
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired();
        validations.Property(validation => validation.HardGlossary).HasColumnName("hard_glossary");
        validations.Property(validation => validation.RecordedAt).HasColumnName("recorded_at");
    }

    /// <summary>Hallazgos del texto original (<c>HALLAZGO</c> del modelo de datos). Se borran con su documento.</summary>
    private static void ConfigureFindings(OwnedNavigationBuilder<Document, Finding> findings)
    {
        findings.ToTable("findings");
        findings.WithOwner().HasForeignKey("DocumentId");
        findings.Property<DocumentId>("DocumentId")
            .HasColumnName("document_id")
            .HasConversion(id => id.Value, value => DocumentId.From(value));
        findings.Property<Guid>("Id").HasColumnName("id").ValueGeneratedOnAdd();
        findings.HasKey("Id");

        findings.Property(finding => finding.GuidelineCode)
            .HasColumnName("guideline_code")
            .HasMaxLength(Guideline.CodeMaxLength)
            .IsRequired();
        findings.HasOne<Guideline>().WithMany().HasForeignKey(finding => finding.GuidelineCode).OnDelete(DeleteBehavior.Restrict);

        findings.Property(finding => finding.Fragment).HasColumnName("fragment").IsRequired();
        findings.Property(finding => finding.Start).HasColumnName("start_position");
        findings.Property(finding => finding.End).HasColumnName("end_position");
        findings.Property(finding => finding.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(GuidelineConfiguration.SeverityMaxLength)
            .IsRequired();
        findings.Property(finding => finding.Suggestion).HasColumnName("suggestion").IsRequired();
    }
}
