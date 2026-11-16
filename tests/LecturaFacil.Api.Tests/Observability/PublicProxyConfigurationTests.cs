// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Api.Tests.Observability;

/// <summary>
/// <c>/metrics</c> no se publica (US-17): el nginx público lo bloquea y la API no expone ningún puerto propio en el
/// compose; solo se llega a ella a través de nginx o desde la red interna de Docker.
/// </summary>
public sealed partial class PublicProxyConfigurationTests
{
    [Fact]
    public void Nginx_BlocksMetricsBeforeAnyOtherLocation()
    {
        var nginx = File.ReadAllText(RepositoryFile("src", "LecturaFacil.Web", "nginx.conf"));

        MetricsBlocked().IsMatch(nginx).ShouldBeTrue("nginx.conf debe tener 'location ^~ /metrics { return 404; }'.");
    }

    [Fact]
    public void Compose_DoesNotPublishAnyPortOfTheApi()
    {
        var compose = File.ReadAllText(RepositoryFile("deploy", "docker-compose.yml"));
        var api = ApiService().Match(compose);

        api.Success.ShouldBeTrue();
        api.Value.ShouldNotContain("ports:");
        api.Value.ShouldNotContain("network_mode: host");
    }

    private static string RepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"No se encuentra {Path.Combine(parts)}.");
    }

    [GeneratedRegex(@"location\s+\^~\s+/metrics\s*\{\s*return\s+404;\s*\}")]
    private static partial Regex MetricsBlocked();

    /// <summary>El bloque del servicio <c>api</c>: desde su nombre hasta el siguiente servicio de primer nivel.</summary>
    [GeneratedRegex(@"^  api:\n(?:(?:    .*)?\n)*", RegexOptions.Multiline)]
    private static partial Regex ApiService();
}
