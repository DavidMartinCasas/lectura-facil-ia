// SPDX-License-Identifier: Apache-2.0
using System.Reflection;
using LecturaFacil.Application.Common;
using LecturaFacil.Infrastructure.Persistence;

namespace LecturaFacil.Architecture.Tests;

/// <summary>Toda opción que guarde un secreto lleva <see cref="SecretAttribute"/> (US-02).</summary>
public sealed class SecretOptionsTests
{
    private static readonly string[] SecretNameHints = ["ApiKey", "Password", "Secret", "ConnectionString"];

    private static readonly Assembly[] Assemblies =
    [
        typeof(Result).Assembly,
        typeof(AppDbContext).Assembly,
        typeof(Program).Assembly,
    ];

    [Fact]
    public void OptionsProperties_WhenNamedLikeSecrets_AreMarkedAsSecret()
    {
        var unmarked = Assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Options", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties())
            .Where(property => SecretNameHints.Any(hint => property.Name.Contains(hint, StringComparison.OrdinalIgnoreCase)))
            .Where(property => !SecretAttribute.IsSecret(property))
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}");

        unmarked.ShouldBeEmpty();
    }

    [Fact]
    public void OptionsTypes_WhenScanned_IncludeTheKnownSecrets()
    {
        var secrets = Assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Options", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties())
            .Where(SecretAttribute.IsSecret)
            .Select(property => property.Name)
            .ToHashSet();

        secrets.ShouldContain("ApiKey");
        secrets.ShouldContain("Password");
        secrets.ShouldContain("ConnectionString");
    }
}
