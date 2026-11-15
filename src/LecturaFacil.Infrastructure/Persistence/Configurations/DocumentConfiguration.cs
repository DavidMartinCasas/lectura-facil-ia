// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
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
            .HasConversion(title => title.Value, value => DocumentTitle.Create(value))
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

        builder.Property(document => document.CreatedAt).HasColumnName("created_at");
        builder.Property(document => document.UpdatedAt).HasColumnName("updated_at");

        // Todas las consultas de documentos filtran por propietario (autorización por objeto).
        builder.HasIndex(document => new { document.OwnerId, document.CreatedAt });
    }
}
