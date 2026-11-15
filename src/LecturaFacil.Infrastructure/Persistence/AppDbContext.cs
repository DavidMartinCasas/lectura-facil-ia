// SPDX-License-Identifier: Apache-2.0
using Microsoft.EntityFrameworkCore;

namespace LecturaFacil.Infrastructure.Persistence;

/// <summary>Contexto de EF Core. Las entidades llegan con las historias que las necesitan.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
