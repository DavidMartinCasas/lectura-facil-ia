// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LecturaFacil.Infrastructure.Persistence.Configurations;

/// <summary>Catálogo de pautas (<c>PAUTA</c> del modelo de datos), sembrado desde <see cref="GuidelineCatalog"/>.</summary>
internal sealed class GuidelineConfiguration : IEntityTypeConfiguration<Guideline>
{
    internal const int SeverityMaxLength = 16;
    private const int DescriptionMaxLength = 200;

    public void Configure(EntityTypeBuilder<Guideline> builder)
    {
        builder.ToTable("guidelines");

        builder.HasKey(guideline => guideline.Code);
        builder.Property(guideline => guideline.Code).HasColumnName("code").HasMaxLength(Guideline.CodeMaxLength);
        builder.Property(guideline => guideline.Description)
            .HasColumnName("description")
            .HasMaxLength(DescriptionMaxLength)
            .IsRequired();
        builder.Property(guideline => guideline.DefaultSeverity)
            .HasColumnName("default_severity")
            .HasConversion<string>()
            .HasMaxLength(SeverityMaxLength)
            .IsRequired();

        builder.HasData(GuidelineCatalog.All);
    }
}
