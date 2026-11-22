// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>
/// Fase de la adaptación mientras el documento está en <see cref="DocumentStatus.Processing"/>. Solo sirve para enseñar
/// el progreso: no cambia el ciclo de vida del documento.
/// </summary>
public enum ProcessingStage
{
    /// <summary>El modelo adaptador está escribiendo la versión en lectura fácil.</summary>
    Adapting,

    /// <summary>Se comprueban las pautas de lectura fácil de la adaptación.</summary>
    ReviewingGuidelines,

    /// <summary>La adaptación incumplía alguna pauta grave y se pide otra.</summary>
    Retrying,

    /// <summary>Otro modelo comprueba que no se pierdan plazos, importes ni requisitos.</summary>
    Verifying,
}
