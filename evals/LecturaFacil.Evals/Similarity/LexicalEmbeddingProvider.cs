// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;

namespace LecturaFacil.Evals.Similarity;

/// <summary>
/// "Embedding" léxico y determinista para <c>--dry-run</c>: cuenta las palabras del texto en un vector de tamaño fijo
/// (con un hash FNV-1a estable entre ejecuciones). No mide el significado: solo sirve para que la herramienta funcione
/// de principio a fin sin ningún modelo.
/// </summary>
public sealed class LexicalEmbeddingProvider : IEmbeddingProvider
{
    public const int Dimensions = 256;

    private const uint FnvOffset = 2_166_136_261;
    private const uint FnvPrime = 16_777_619;

    public string Model => "léxico (simulado)";

    public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Embed(text));
    }

    public static IReadOnlyList<float> Embed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var vector = new float[Dimensions];
        foreach (var word in Words(text))
        {
            vector[Hash(word) % Dimensions]++;
        }

        return vector;
    }

    private static IEnumerable<string> Words(string text)
    {
        var word = new StringBuilder();
        foreach (var character in text.ToLower(CultureInfo.InvariantCulture))
        {
            if (char.IsLetterOrDigit(character))
            {
                word.Append(character);
            }
            else if (word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            yield return word.ToString();
        }
    }

    private static uint Hash(string word)
    {
        var hash = FnvOffset;
        foreach (var character in word)
        {
            hash = (hash ^ character) * FnvPrime;
        }

        return hash;
    }
}
