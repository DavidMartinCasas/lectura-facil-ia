// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Adaptation;

/// <summary>Cola de documentos pendientes de adaptar. Un proceso en segundo plano la consume con <see cref="AdaptDocument"/>.</summary>
public interface IAdaptationQueue
{
    /// <summary>Pone el documento en la cola. Si ya estaba pendiente, no lo repite.</summary>
    ValueTask EnqueueAsync(DocumentId id, CancellationToken cancellationToken);
}
