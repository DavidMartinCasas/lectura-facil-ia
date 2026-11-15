// SPDX-License-Identifier: Apache-2.0
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
            throw new OptionsValidationException(Options.DefaultName, typeof(StartupValidation), failures);
        }
    }

    private static bool TryGetFailures(Exception exception, out string[] failures)
    {
        IEnumerable<Exception> exceptions = exception is AggregateException aggregate
            ? aggregate.InnerExceptions
            : [exception];
        var validationErrors = exceptions.OfType<OptionsValidationException>().ToArray();

        failures = [.. validationErrors.SelectMany(error => error.Failures)];
        return validationErrors.Length > 0;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Configuración no válida; la API no arranca. {Failures}")]
    private static partial void InvalidConfiguration(ILogger logger, string failures);
}
