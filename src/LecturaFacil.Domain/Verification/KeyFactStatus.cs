// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Verification;

/// <summary>Estado de un dato clave en la adaptación, de menos a más grave. El orden importa: manda el más grave.</summary>
public enum KeyFactStatus
{
    /// <summary>Está en la adaptación con el mismo valor.</summary>
    Conserved = 0,

    /// <summary>Ha cambiado, es un cálculo o hay dudas: debe revisarlo una persona.</summary>
    Review = 1,

    /// <summary>No aparece en la adaptación.</summary>
    Omitted = 2,
}
