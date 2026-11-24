// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>Si el verificador ha podido comprobar la adaptación.</summary>
public enum VerificationStatus
{
    /// <summary>El verificador ha respondido y su respuesta se ha combinado con la comprobación por código.</summary>
    Verified,

    /// <summary>
    /// El verificador no ha respondido o su respuesta no era válida: todas las cifras del original quedan para revisar.
    /// </summary>
    NotVerified,

    /// <summary>
    /// Documento largo verificado por partes en el que alguna parte no se ha podido verificar: esa parte queda para revisar
    /// y las demás tienen su resultado.
    /// </summary>
    PartiallyVerified,
}
