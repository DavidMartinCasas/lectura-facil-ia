// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Users;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Evals.Running;

/// <summary>Propietario ficticio de los documentos de la evaluación.</summary>
internal sealed class EvaluationUser : ICurrentUser
{
    public OwnerId OwnerId { get; } = OwnerId.From(Guid.NewGuid());
}

/// <summary>La evaluación adapta cada documento en el momento, sin cola ni proceso en segundo plano.</summary>
internal sealed class NoAdaptationQueue : IAdaptationQueue
{
    public ValueTask EnqueueAsync(DocumentId id, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}

/// <summary>La evaluación mide con <see cref="MeteredLlmProvider"/>; no exporta métricas de producción.</summary>
internal sealed class NoAdaptationTelemetry : IAdaptationTelemetry
{
    public void Record(AdaptationOutcome outcome)
    {
    }
}
