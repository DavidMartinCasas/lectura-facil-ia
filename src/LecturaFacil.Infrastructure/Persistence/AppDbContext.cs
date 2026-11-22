// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Identity;
using LecturaFacil.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LecturaFacil.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core: documentos y usuarios de Identity (sin roles: llegan, si hacen falta, en la versión 2). Las
/// entidades llegan con las historias que las necesitan.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // Búsqueda por título sin distinguir tildes en "Mis documentos". Es una extensión de confianza en PostgreSQL 13+:
        // la puede crear el propietario de la base de datos sin ser superusuario.
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.ApplyIdentityTableNames();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
