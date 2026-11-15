// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>Carga los prompts de <c>Llm/Prompts/&lt;nombre&gt;.v&lt;N&gt;.md</c>, embebidos en el ensamblado.</summary>
public sealed partial class EmbeddedPromptLoader : IPromptLoader
{
    public const string ResourcePrefix = "LecturaFacil.Infrastructure.Llm.Prompts.";

    private readonly ConcurrentDictionary<string, Prompt> _cache = new(StringComparer.Ordinal);

    public Prompt Load(string name)
    {
        if (string.IsNullOrEmpty(name) || !PromptName().IsMatch(name))
        {
            throw new ArgumentException("Prompt names look like 'adapter.v1'.", nameof(name));
        }

        return _cache.GetOrAdd(name, Read);
    }

    private static Prompt Read(string name)
    {
        using var stream = typeof(EmbeddedPromptLoader).Assembly.GetManifestResourceStream($"{ResourcePrefix}{name}.md")
            ?? throw new InvalidOperationException($"Prompt '{name}' not found.");
        using var reader = new StreamReader(stream);
        return new Prompt(name, reader.ReadToEnd().Trim());
    }

    [GeneratedRegex(@"^[a-z0-9-]+\.v[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PromptName();
}
