// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Api.Tests.Configuration;

/// <summary>Lee las opciones de la API (las variables con <c>__</c>) de <c>deploy/.env.example</c>.</summary>
internal static class EnvExample
{
    public static Dictionary<string, string?> ReadApiSettings()
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(FindFile()))
        {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf('=', StringComparison.Ordinal);
            if (trimmed.StartsWith('#') || separator <= 0 || !trimmed[..separator].Contains("__", StringComparison.Ordinal))
            {
                continue;
            }

            var key = trimmed[..separator].Replace("__", ":", StringComparison.Ordinal);
            settings[key] = trimmed[(separator + 1)..].Trim('"');
        }

        return settings;
    }

    private static string FindFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "deploy", ".env.example");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("No se encuentra deploy/.env.example.");
    }
}
