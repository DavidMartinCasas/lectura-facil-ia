// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Opciones comunes de las expresiones regulares de las reglas. Todas tienen un tiempo máximo por búsqueda para que
/// un texto malicioso no pueda bloquear el análisis (ReDoS); si se agota, el analizador descarta esa regla.
/// </summary>
internal static class GuidelineRegex
{
    public const int TimeoutMilliseconds = 250;

    public const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    public const RegexOptions IgnoreCase = Options | RegexOptions.IgnoreCase;

    public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(TimeoutMilliseconds);
}
