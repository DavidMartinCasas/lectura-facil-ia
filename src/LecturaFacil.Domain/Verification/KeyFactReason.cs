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

    /// <summary>El dato, o el valor que cita el verificador, no está en la adaptación.</summary>
    NotInAdaptation,

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
        KeyFactReason.NotVerified => "No se ha podido verificar; debe revisarlo una persona",
        _ => "Debe revisarlo una persona",
    };
}
