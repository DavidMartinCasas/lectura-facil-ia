// SPDX-License-Identifier: Apache-2.0
using System.Reflection;
using LecturaFacil.Infrastructure.Observability;

namespace LecturaFacil.Api.Observability;

/// <summary>Versión de la aplicación que se publica en <c>/health</c> y en Sentry (US-17).</summary>
internal sealed record AppVersion(string Value)
{
    public const string Unknown = "unknown";

    /// <summary><see cref="ObservabilityOptions.Release"/> (el <c>git sha</c> del despliegue) o la versión compilada.</summary>
    public static AppVersion From(ObservabilityOptions options) =>
        new(string.IsNullOrEmpty(options.Release) ? CompiledVersion() : options.Release);

    private static string CompiledVersion() =>
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Unknown;
}
