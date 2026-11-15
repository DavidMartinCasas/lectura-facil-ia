// SPDX-License-Identifier: Apache-2.0
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LecturaFacil.Infrastructure.Persistence;

/// <summary>
/// Solo para <c>dotnet ef migrations add</c>: genera migraciones sin conectarse a ninguna base de datos,
/// por eso la cadena no lleva credenciales.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=lecturafacil").Options);
}
