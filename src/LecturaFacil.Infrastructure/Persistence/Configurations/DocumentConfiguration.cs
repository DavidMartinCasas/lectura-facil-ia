// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

        builder.Property(document => document.FailureCode)
            .HasColumnName("failure_code")
            .HasMaxLength(Document.FailureCodeMaxLength);

        builder.Property(document => document.CreatedAt).HasColumnName("created_at");
        builder.Property(document => document.UpdatedAt).HasColumnName("updated_at");

        // Todas las consultas de documentos filtran por propietario (autorización por objeto).
        builder.HasIndex(document => new { document.OwnerId, document.CreatedAt });

        // El worker busca al arrancar los documentos que esperan su adaptación.
        builder.HasIndex(document => document.Status);

        builder.OwnsMany(document => document.OriginalFindings, ConfigureFindings);
        builder.Navigation(document => document.OriginalFindings).HasField("_originalFindings");

        builder.OwnsMany(document => document.AdaptedVersions, AdaptedVersionConfiguration.Configure);
        builder.Navigation(document => document.AdaptedVersions).HasField("_adaptedVersions");
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
