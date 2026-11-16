// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Puerto para registrar cómo ha ido cada adaptación (log estructurado y métricas). Es el único sitio que define qué
/// se registra de una adaptación: solo lo que lleva <see cref="AdaptationOutcome"/>, nunca el texto (US-17).
/// </summary>
public interface IAdaptationTelemetry
{
    void Record(AdaptationOutcome outcome);
}
