// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Motivo de un aviso. Es una lista cerrada con textos fijos: el motivo que se muestra nunca es texto libre del modelo.
/// </summary>
public enum KeyFactReason
{
    /// <summary>Sin aviso: el dato se conserva.</summary>
    None,

    /// <summary>La cifra de la adaptación no es la del original.</summary>
    ValueChanged,

    /// <summary>La adaptación da un valor calculado a partir del original (por ejemplo, del IPREM a euros).</summary>
    CalculatedConversion,

    /// <summary>El dato no está en la adaptación.</summary>
    NotInAdaptation,

    /// <summary>El texto que cita el verificador no está en la adaptación (o sus cifras no están).</summary>
    QuoteNotFound,

    /// <summary>Una cifra del original, que el verificador no ha listado, no está en la adaptación.</summary>
    FigureNotInAdaptation,

    /// <summary>La cifra se conserva, pero ha cambiado lo que la acompaña: unidad, periodo, máximo o mínimo, días hábiles.</summary>
    QualifierChanged,

    /// <summary>El verificador no ha respondido o su respuesta no era válida.</summary>
    NotVerified,

    /// <summary>Cualquier otra duda.</summary>
    NeedsReview,
}

/// <summary>Textos para el usuario de cada <see cref="KeyFactReason"/>.</summary>
public static class KeyFactReasons
{
    public static string Message(KeyFactReason reason) => reason switch
    {
        KeyFactReason.None => string.Empty,
        KeyFactReason.ValueChanged => "El valor ha cambiado",
        KeyFactReason.CalculatedConversion => "Conversión calculada; debe confirmarla una persona",
        KeyFactReason.NotInAdaptation => "No aparece en la adaptación",
        KeyFactReason.QuoteNotFound => "El texto citado no está en la adaptación. Compárelo con el original.",
        KeyFactReason.FigureNotInAdaptation => "Esta cifra del original no está en la adaptación",
        KeyFactReason.QualifierChanged => "Ha cambiado lo que acompaña a la cifra: la unidad, el periodo, el máximo o mínimo, o si los días son hábiles",
        KeyFactReason.NotVerified => "No se ha podido verificar; debe revisarlo una persona",
        _ => "Hay dudas sobre este dato. Compárelo con el original.",
    };
}
