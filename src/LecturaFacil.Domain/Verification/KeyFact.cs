// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Dato clave del original (plazo, importe, requisito…) y cómo ha quedado en la adaptación (<c>DATO_CLAVE</c> del
/// modelo de datos). Solo informa: nunca cambia la adaptación.
/// </summary>
public sealed record KeyFact
{
    /// <summary>Longitud máxima del valor original y del adaptado.</summary>
    public const int ValueMaxLength = 500;

    private KeyFact(KeyFactType type, string originalValue, string? adaptedValue, KeyFactVerdict verdict)
    {
        Type = type;
        OriginalValue = originalValue;
        AdaptedValue = adaptedValue;
        Status = verdict.Status;
        Reason = verdict.Reason;
    }

    public KeyFactType Type { get; }

    /// <summary>El dato tal como está en el original.</summary>
    public string OriginalValue { get; }

    /// <summary>Dónde está en la adaptación; <c>null</c> si no aparece.</summary>
    public string? AdaptedValue { get; }

    public KeyFactStatus Status { get; }

    public KeyFactReason Reason { get; }

    /// <summary>Por ejemplo, "El valor ha cambiado". Vacío si el dato se conserva.</summary>
    public string ReasonMessage => KeyFactReasons.Message(Reason);

    public KeyFactVerdict Verdict => new(Status, Reason);

    /// <summary>Crea un dato clave. El veredicto se normaliza con <see cref="KeyFactVerdict.Create"/>.</summary>
    public static KeyFact Create(KeyFactType type, string originalValue, string? adaptedValue, KeyFactVerdict verdict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalValue);
        if (originalValue.Length > ValueMaxLength || adaptedValue?.Length > ValueMaxLength)
        {
            throw new ArgumentException($"Key fact values cannot exceed {ValueMaxLength} characters.", nameof(originalValue));
        }

        var adapted = string.IsNullOrWhiteSpace(adaptedValue) ? null : adaptedValue.Trim();
        var normalizedType = Enum.IsDefined(type) ? type : KeyFactType.Other;
        return new KeyFact(normalizedType, originalValue.Trim(), adapted, KeyFactVerdict.Create(verdict.Status, verdict.Reason));
    }

    /// <summary>Corta un valor a <see cref="ValueMaxLength"/> sin partir un par sustituto UTF-16 (un emoji, por ejemplo).</summary>
    public static string Truncate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length <= ValueMaxLength)
        {
            return value;
        }

        var length = char.IsHighSurrogate(value[ValueMaxLength - 1]) ? ValueMaxLength - 1 : ValueMaxLength;
        return value[..length];
    }

    internal KeyFact WithVerdict(KeyFactVerdict verdict) => Create(Type, OriginalValue, AdaptedValue, verdict);
}
