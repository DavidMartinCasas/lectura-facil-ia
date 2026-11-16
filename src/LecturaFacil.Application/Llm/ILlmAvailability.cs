// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Application.Llm;

/// <summary>Disponibilidad de los proveedores según la última comprobación de salud.</summary>
public interface ILlmAvailability
{
    LlmAvailabilityStatus Current { get; }
}

/// <summary>Solo disponible o no disponible: sin URLs, modelos ni detalles internos.</summary>
/// <param name="PrimaryAvailable">Si el proveedor principal respondió a la última comprobación.</param>
/// <param name="FallbackAvailable">Igual para el respaldo; <c>null</c> si el respaldo no está habilitado.</param>
public sealed record LlmAvailabilityStatus(bool PrimaryAvailable, bool? FallbackAvailable);
