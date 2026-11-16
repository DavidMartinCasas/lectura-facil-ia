// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>Qué clase de dato clave es (<c>DATO_CLAVE.tipo</c>).</summary>
public enum KeyFactType
{
    /// <summary>Plazo: "quince días hábiles".</summary>
    Deadline,

    /// <summary>Importe o cantidad: "máximo de 250 €", "40 % de la renta".</summary>
    Amount,

    /// <summary>Requisito: "no ser titular de otra vivienda".</summary>
    Requirement,

    /// <summary>Fecha u horario.</summary>
    Date,

    /// <summary>Lugar, dirección o teléfono.</summary>
    Place,

    /// <summary>Documento que hay que presentar.</summary>
    Document,

    /// <summary>Referencia a una norma o a otro texto: "Ordenanza Municipal 7/2024".</summary>
    Reference,

    /// <summary>Cifra del original que encuentra el código y que el verificador no ha listado.</summary>
    Figure,

    /// <summary>Cualquier otro dato importante.</summary>
    Other,
}
