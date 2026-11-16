// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LecturaFacil.Infrastructure.Tests.Observability;

/// <summary>Logger que guarda cada entrada con sus propiedades estructuradas, para comprobarlas.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        _entries.Enqueue(new LogEntry(logLevel, eventId.Name, formatter(state, exception), properties));
    }
}

internal sealed record LogEntry(LogLevel Level, string? EventName, string Message, IReadOnlyDictionary<string, object?> Properties);
