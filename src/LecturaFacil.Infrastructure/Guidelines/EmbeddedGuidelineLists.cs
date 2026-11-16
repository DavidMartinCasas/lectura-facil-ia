// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Infrastructure.Guidelines;

/// <summary>
/// Listas de las reglas de pautas, embebidas en el ensamblado como JSON (<c>Guidelines/Lists/*.json</c>).
/// Para ampliarlas basta con editar esos ficheros.
/// </summary>
public static class EmbeddedGuidelineLists
{
    public const string ResourcePrefix = "LecturaFacil.Infrastructure.Guidelines.Lists.";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<string> AllowedAcronyms() => Read<List<string>>("allowed-acronyms.json");

    public static IReadOnlyList<TermSuggestion> LegalJargon() => Read<List<TermSuggestion>>("legal-jargon.json");

    public static IReadOnlyList<TermSuggestion> DifficultWords() => Read<List<TermSuggestion>>("difficult-words.json");

    private static T Read<T>(string fileName)
    {
        using var stream = typeof(EmbeddedGuidelineLists).Assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException($"Guideline list '{fileName}' not found.");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Guideline list '{fileName}' is empty.");
    }
}
