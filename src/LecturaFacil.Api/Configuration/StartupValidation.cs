// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Configuration;

/// <summary>
/// Valida todas las opciones antes de usar ninguna. Si algo falla, registra qué opciones fallan
/// (nunca sus valores) y lanza una única <see cref="OptionsValidationException"/>: la API no arranca.
/// </summary>
internal static partial class StartupValidation
{
    public static void ValidateOptionsOrStop(this WebApplication app)
    {
        try
        {
            app.Services.GetRequiredService<IStartupValidator>().Validate();
        }
        catch (Exception exception) when (TryGetFailures(exception, out var failures))
        {
            InvalidConfiguration(app.Logger, string.Join(" | ", failures));
            throw new OptionsValidationException(Options.DefaultName, typeof(object), failures);
        }
    }

    private static bool TryGetFailures(Exception exception, out string[] failures)
    {
        IEnumerable<Exception> exceptions = exception is AggregateException aggregate
            ? aggregate.InnerExceptions
            : [exception];

        failures = [.. exceptions.SelectMany(Describe)];
        return failures.Length > 0;
    }

    private static IEnumerable<string> Describe(Exception exception) => exception switch
    {
        OptionsValidationException validation => validation.Failures,

        // El mensaje del binder incluye el valor: solo se conserva la ruta de la opción.
        InvalidOperationException conversion when ConversionFailure().Match(conversion.Message) is { Success: true } match =>
            [$"{match.Groups["path"].Value} tiene un valor con un formato no válido."],
        _ => [],
    };

    [GeneratedRegex(@"^Failed to convert configuration value .* at '(?<path>[^']+)' to type ", RegexOptions.Singleline)]
    private static partial Regex ConversionFailure();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Configuración no válida; la API no arranca. {Failures}")]
    private static partial void InvalidConfiguration(ILogger logger, string failures);
}
