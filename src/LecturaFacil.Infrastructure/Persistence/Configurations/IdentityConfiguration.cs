// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LecturaFacil.Infrastructure.Persistence.Configurations;

/// <summary>Tablas de Identity con los nombres en minúsculas del resto del esquema. Las columnas son las de Identity.</summary>
internal static class IdentityConfiguration
{
    public static ModelBuilder ApplyIdentityTableNames(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().ToTable("users");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        return modelBuilder;
    }
}
