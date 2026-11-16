// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Infrastructure.Adaptation;

/// <summary>
/// Cola en memoria de documentos por adaptar. Solo guarda identificadores; si el servicio se para, los documentos
/// pendientes siguen en la base de datos y <see cref="RequeuePendingAdaptations"/> los vuelve a poner en la cola.
/// Un documento que ya está esperando no se repite.
/// </summary>
public sealed class ChannelAdaptationQueue : IAdaptationQueue
{
    private readonly Channel<DocumentId> _channel = Channel.CreateUnbounded<DocumentId>();
    private readonly ConcurrentDictionary<DocumentId, byte> _waiting = new();

    public ValueTask EnqueueAsync(DocumentId id, CancellationToken cancellationToken) =>
        _waiting.TryAdd(id, 0) ? _channel.Writer.WriteAsync(id, cancellationToken) : ValueTask.CompletedTask;

    /// <summary>Saca documentos de la cola según llegan. Varios lectores pueden leer a la vez: cada documento sale una vez.</summary>
    public async IAsyncEnumerable<DocumentId> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var id in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            _waiting.TryRemove(id, out _);
            yield return id;
        }
    }
}
