// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Adaptation;

/// <summary>
/// Proceso en segundo plano que adapta los documentos de la cola. Solo orquesta: la lógica está en
/// <see cref="AdaptDocument"/>. Al arrancar vuelve a poner en la cola los documentos pendientes y procesa hasta
/// <see cref="LimitsOptions.AdaptationConcurrency"/> documentos a la vez. Un fallo con un documento nunca para el
/// proceso. Solo registra identificadores, códigos de error y tiempos; nunca el texto. Todo lo que se registra mientras
/// se procesa un documento lleva su <c>DocumentId</c> (US-17); el resultado de cada adaptación lo registra
/// <see cref="IAdaptationTelemetry"/>.
/// </summary>
public sealed partial class AdaptationWorker(
    ChannelAdaptationQueue queue,
    IServiceScopeFactory scopeFactory,
    IAdaptationTelemetry telemetry,
    IOptions<LimitsOptions> limits,
    ILogger<AdaptationWorker> logger) : BackgroundService
{
    /// <summary>Propiedad del ámbito de log con el identificador del documento que se está procesando.</summary>
    public const string DocumentIdProperty = "DocumentId";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);
        var consumers = Enumerable.Range(0, limits.Value.AdaptationConcurrency).Select(_ => ConsumeAsync(stoppingToken));
        await Task.WhenAll(consumers);
    }

    private async Task RequeuePendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var requeued = await scope.ServiceProvider.GetRequiredService<RequeuePendingAdaptations>().ExecuteAsync(stoppingToken);
            LogRequeued(requeued);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // El servicio se está parando.
        }
        catch (Exception exception)
        {
            // Sin base de datos al arrancar: los documentos nuevos se siguen adaptando y los pendientes, al reiniciar.
            LogRequeueFailed(exception.GetType().Name);
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var id in queue.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(id, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Un documento a medias se queda en "Procesando" y se vuelve a poner en la cola al arrancar.
        }
    }

    private async Task ProcessAsync(DocumentId id, CancellationToken stoppingToken)
    {
        using var documentScope = logger.BeginScope(new Dictionary<string, object> { [DocumentIdProperty] = id.Value });
        var started = Stopwatch.GetTimestamp();
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AdaptDocument>().ExecuteAsync(id, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            // Solo el tipo: el mensaje de una excepción podría contener parte del documento.
            LogUnexpectedFailure(id.Value, exception.GetType().Name);
            telemetry.Record(AdaptationOutcome.Unexpected(id, Stopwatch.GetElapsedTime(started)));
            await MarkAsFailedAsync(id, stoppingToken);
        }
    }

    /// <summary>Intenta dejar el documento en "Error". Si tampoco se puede, se queda en "Procesando" y se reencola al arrancar.</summary>
    private async Task MarkAsFailedAsync(DocumentId id, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AdaptDocument>().FailUnexpectedlyAsync(id, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            LogMarkAsFailedFailed(id.Value, exception.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Adaptation queue: {Count} pending documents requeued on startup.")]
    private partial void LogRequeued(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Adaptation queue: pending documents could not be requeued on startup ({ExceptionType}).")]
    private partial void LogRequeueFailed(string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Document {DocumentId}: unexpected failure while adapting ({ExceptionType}).")]
    private partial void LogUnexpectedFailure(Guid documentId, string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Document {DocumentId} could not be marked as failed ({ExceptionType}); it will be requeued on startup.")]
    private partial void LogMarkAsFailedFailed(Guid documentId, string exceptionType);
}
