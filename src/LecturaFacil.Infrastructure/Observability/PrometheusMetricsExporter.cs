// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace LecturaFacil.Infrastructure.Observability;

/// <summary>
/// Escucha los instrumentos de <see cref="System.Diagnostics.Metrics"/> de la aplicación (los medidores cuyo nombre
/// empieza por <see cref="MeterPrefix"/>, creados con el <see cref="IMeterFactory"/> de este contenedor) y los escribe
/// en el formato de texto de Prometheus. Admite contadores enteros e histogramas, que es lo que usa la aplicación.
/// </summary>
/// <remarks>
/// Es pequeño a propósito: el exportador oficial de OpenTelemetry para Prometheus sigue en beta y no hace falta una
/// dependencia más para unos pocos contadores. Las etiquetas solo llevan valores acotados (rol, resultado, código de
/// error); nunca identificadores de documentos ni texto.
/// </remarks>
public sealed class PrometheusMetricsExporter : IDisposable
{
    public const string MeterPrefix = "LecturaFacil";
    public const string ContentType = "text/plain; version=0.0.4; charset=utf-8";

    /// <summary>Límites superiores de los cubos de los histogramas, en la unidad del instrumento (segundos).</summary>
    public static readonly IReadOnlyList<double> HistogramBuckets = [0.1, 0.5, 1, 2.5, 5, 10, 30, 60, 120, 300, 600];

    private readonly MeterListener _listener = new();
    private readonly ConcurrentDictionary<Instrument, InstrumentSeries> _instruments = new();

    public PrometheusMetricsExporter(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, meterFactory)
                && instrument.Meter.Name.StartsWith(MeterPrefix, StringComparison.Ordinal)
                && instrument is Counter<long> or Histogram<double>)
            {
                listener.EnableMeasurementEvents(instrument, _instruments.GetOrAdd(instrument, InstrumentSeries.For));
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, state) => ((InstrumentSeries)state!).Add(value, tags));
        _listener.SetMeasurementEventCallback<double>((_, value, tags, state) => ((InstrumentSeries)state!).Add(value, tags));
        _listener.Start();
    }

    /// <summary>Todas las series en el formato de texto de Prometheus 0.0.4.</summary>
    public string Render()
    {
        var output = new StringBuilder();
        foreach (var series in _instruments.Values.OrderBy(series => series.Name, StringComparer.Ordinal))
        {
            series.WriteTo(output);
        }

        return output.ToString();
    }

    public void Dispose() => _listener.Dispose();

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Etiquetas ordenadas por nombre, ya escritas: <c>a="1",b="2"</c>.</summary>
    private static string Labels(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var pairs = new List<string>(tags.Length);
        foreach (var tag in tags)
        {
            pairs.Add($"{tag.Key}=\"{Escape(Convert.ToString(tag.Value, CultureInfo.InvariantCulture) ?? string.Empty)}\"");
        }

        pairs.Sort(StringComparer.Ordinal);
        return string.Join(',', pairs);
    }

    private static string WithLabels(string labels, string? extra = null)
    {
        var all = string.Join(',', new[] { labels, extra }.Where(part => !string.IsNullOrEmpty(part)));
        return all.Length == 0 ? string.Empty : $"{{{all}}}";
    }

    private abstract class InstrumentSeries(string name, string? description)
    {
        public string Name { get; } = name;

        public static InstrumentSeries For(Instrument instrument)
        {
            var baseName = instrument.Name.Replace('.', '_').Replace('-', '_');
            var unit = instrument.Unit == "s" ? "_seconds" : string.Empty;
            return instrument is Histogram<double>
                ? new HistogramSeries($"{baseName}{unit}", instrument.Description)
                : new CounterSeries($"{baseName}{unit}_total", instrument.Description);
        }

        public abstract void Add(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

        public void WriteTo(StringBuilder output)
        {
            if (!string.IsNullOrEmpty(description))
            {
                output.Append(CultureInfo.InvariantCulture, $"# HELP {Name} {description.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}\n");
            }

            WriteSeries(output);
        }

        protected abstract void WriteSeries(StringBuilder output);
    }

    private sealed class CounterSeries(string name, string? description) : InstrumentSeries(name, description)
    {
        private readonly ConcurrentDictionary<string, double> _values = new(StringComparer.Ordinal);

        public override void Add(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
            _values.AddOrUpdate(Labels(tags), value, (_, current) => current + value);

        protected override void WriteSeries(StringBuilder output)
        {
            output.Append(CultureInfo.InvariantCulture, $"# TYPE {Name} counter\n");
            foreach (var (labels, value) in _values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                output.Append(CultureInfo.InvariantCulture, $"{Name}{WithLabels(labels)} {Number(value)}\n");
            }
        }
    }

    private sealed class HistogramSeries(string name, string? description) : InstrumentSeries(name, description)
    {
        private readonly ConcurrentDictionary<string, Distribution> _values = new(StringComparer.Ordinal);

        public override void Add(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
            _values.GetOrAdd(Labels(tags), _ => new Distribution()).Add(value);

        protected override void WriteSeries(StringBuilder output)
        {
            output.Append(CultureInfo.InvariantCulture, $"# TYPE {Name} histogram\n");
            foreach (var (labels, distribution) in _values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var (buckets, sum, count) = distribution.Snapshot();
                var cumulative = 0L;
                for (var index = 0; index < HistogramBuckets.Count; index++)
                {
                    cumulative += buckets[index];
                    output.Append(CultureInfo.InvariantCulture, $"{Name}_bucket{WithLabels(labels, $"le=\"{Number(HistogramBuckets[index])}\"")} {cumulative}\n");
                }

                output.Append(CultureInfo.InvariantCulture, $"{Name}_bucket{WithLabels(labels, "le=\"+Inf\"")} {count}\n");
                output.Append(CultureInfo.InvariantCulture, $"{Name}_sum{WithLabels(labels)} {Number(sum)}\n");
                output.Append(CultureInfo.InvariantCulture, $"{Name}_count{WithLabels(labels)} {count}\n");
            }
        }
    }

    private sealed class Distribution
    {
        private readonly Lock _lock = new();
        private readonly long[] _buckets = new long[HistogramBuckets.Count];
        private double _sum;
        private long _count;

        public void Add(double value)
        {
            lock (_lock)
            {
                var index = 0;
                while (index < HistogramBuckets.Count && value > HistogramBuckets[index])
                {
                    index++;
                }

                if (index < HistogramBuckets.Count)
                {
                    _buckets[index]++;
                }

                _sum += value;
                _count++;
            }
        }

        public (long[] Buckets, double Sum, long Count) Snapshot()
        {
            lock (_lock)
            {
                return ((long[])_buckets.Clone(), _sum, _count);
            }
        }
    }
}
