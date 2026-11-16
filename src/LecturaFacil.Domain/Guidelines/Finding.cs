// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Fragmento de un texto que incumple una pauta. <see cref="Start"/> y <see cref="End"/> son índices de carácter
/// (UTF-16) sobre el texto analizado, con el fin excluido: <c>text[Start..End] == Fragment</c>.
/// </summary>
public sealed record Finding
{
    private Finding(string guidelineCode, string fragment, int start, int end, Severity severity, string suggestion)
    {
        GuidelineCode = guidelineCode;
        Fragment = fragment;
        Start = start;
        End = end;
        Severity = severity;
        Suggestion = suggestion;
    }

    public string GuidelineCode { get; }

    public string Fragment { get; }

    public int Start { get; }

    public int End { get; }

    public Severity Severity { get; }

    /// <summary>Qué hacer para cumplir la pauta, en español y para la persona que adapta el texto.</summary>
    public string Suggestion { get; }

    /// <summary>Crea un hallazgo sobre <c>text[start..end]</c> con la gravedad por defecto de la pauta.</summary>
    public static Finding At(Guideline guideline, string text, int start, int end, string suggestion)
    {
        ArgumentNullException.ThrowIfNull(guideline);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestion);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(end, start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, text.Length);

        return new Finding(guideline.Code, text[start..end], start, end, guideline.DefaultSeverity, suggestion);
    }

    /// <summary>Reconstruye un hallazgo ya guardado, sin volver a comprobarlo contra el texto.</summary>
    public static Finding Restore(string guidelineCode, string fragment, int start, int end, Severity severity, string suggestion) =>
        new(guidelineCode, fragment, start, end, severity, suggestion);
}
