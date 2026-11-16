// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;

namespace LecturaFacil.Application.Export;

/// <summary>
/// Nombre del fichero de descarga a partir del título: solo letras ASCII minúsculas, cifras y guiones. Así no puede
/// llevar rutas, saltos de línea ni comillas que rompan la cabecera <c>Content-Disposition</c>.
/// </summary>
public static class ExportFileName
{
    /// <summary>Caracteres como mucho del nombre, sin la extensión.</summary>
    public const int MaxBaseLength = 60;

    /// <summary>Nombre que se usa si del título no queda ningún carácter válido.</summary>
    public const string Fallback = "documento";

    public static string Create(string title, string fileExtension)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);

        var slug = Slugify(title);
        if (slug.Length > MaxBaseLength)
        {
            // Se corta en el último guion para no dejar una palabra a medias, salvo que sea una sola palabra muy larga.
            var cut = slug[..(MaxBaseLength + 1)].LastIndexOf('-');
            slug = cut > 0 ? slug[..cut] : slug[..MaxBaseLength];
        }

        return $"{(slug.Length == 0 ? Fallback : slug)}.{fileExtension}";
    }

    private static string Slugify(string title)
    {
        var builder = new StringBuilder(title.Length);
        var pendingHyphen = false;
        foreach (var character in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = true;
            }
        }

        return builder.ToString();
    }
}
