// SPDX-License-Identifier: Apache-2.0
using System.Reflection;
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace LecturaFacil.Architecture.Tests;

/// <summary>Regla de dependencias de Clean Architecture (AGENTS.md, sección 4). No desactivar.</summary>
public sealed class DependencyRuleTests
{
    private const string DomainNamespace = "LecturaFacil.Domain";
    private const string ApplicationNamespace = "LecturaFacil.Application";
    private const string InfrastructureNamespace = "LecturaFacil.Infrastructure";
    private const string ApiNamespace = "LecturaFacil.Api";

    private static readonly Assembly DomainAssembly = typeof(DocumentId).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Result).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;

    [Fact]
    public void Domain_WhenInspected_DoesNotDependOnOtherLayersOrFrameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace, "Microsoft")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Domain_WhenInspected_DoesNotReferenceOtherProjectAssemblies()
    {
        ReferencedProjectAssemblies(DomainAssembly).ShouldBeEmpty();
    }

    [Fact]
    public void Application_WhenInspected_DoesNotDependOnInfrastructureApiOrAspNetCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureNamespace,
                ApiNamespace,
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_WhenInspected_ReferencesNoProjectOtherThanDomain()
    {
        ReferencedProjectAssemblies(ApplicationAssembly).ShouldAllBe(name => name == DomainNamespace);
    }

    [Fact]
    public void Infrastructure_WhenInspected_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        AssertSuccessful(result);
    }

    private static string[] ReferencedProjectAssemblies(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("LecturaFacil.", StringComparison.Ordinal))];

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful.ShouldBeTrue(
            $"Tipos que rompen la regla: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
