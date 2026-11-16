// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Documents;

namespace LecturaFacil.Application.Adaptation;

/// <summary>
/// Al arrancar, vuelve a poner en la cola los documentos que esperaban su adaptación: los que estaban en
/// "Procesando" cuando se paró el servicio y los recibidos que aún no se habían empezado.
/// </summary>
public sealed class RequeuePendingAdaptations(IDocumentRepository repository, IAdaptationQueue queue)
{
    /// <returns>Cuántos documentos se han puesto en la cola.</returns>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var pending = await repository.ListAwaitingAdaptationAsync(cancellationToken);
        foreach (var id in pending)
        {
            await queue.EnqueueAsync(id, cancellationToken);
        }

        return pending.Count;
    }
}
