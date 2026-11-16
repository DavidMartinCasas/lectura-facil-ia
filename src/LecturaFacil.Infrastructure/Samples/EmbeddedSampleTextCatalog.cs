// SPDX-License-Identifier: Apache-2.0
using System.Text.Json;
using LecturaFacil.Application.Samples;

namespace LecturaFacil.Infrastructure.Samples;

/// <summary>
/// Los 3 textos de ejemplo (US-16), embebidos en el ensamblado (<c>Samples/sample-texts.json</c>). Son ficticios, no
/// contienen datos personales y declaran su fuente. Se leen una vez y no cambian.
/// </summary>
public sealed class EmbeddedSampleTextCatalog : ISampleTextCatalog
{
    public const string ResourceName = "LecturaFacil.Infrastructure.Samples.sample-texts.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<IReadOnlyList<SampleText>> Samples = new(Read);

    public IReadOnlyList<SampleText> All() => Samples.Value;

    private static IReadOnlyList<SampleText> Read()
    {
        using var stream = typeof(EmbeddedSampleTextCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Sample texts '{ResourceName}' not found.");
        return JsonSerializer.Deserialize<List<SampleText>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Sample texts '{ResourceName}' are empty.");
    }
}
