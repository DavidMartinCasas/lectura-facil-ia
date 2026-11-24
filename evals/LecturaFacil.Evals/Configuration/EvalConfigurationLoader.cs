// SPDX-License-Identifier: Apache-2.0
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using LecturaFacil.Application.Common;
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Evals.Configuration;

/// <summary>
/// Lee los perfiles de <c>evals/modelos.json</c> que se han pedido y los valida con las mismas reglas que la
/// configuración de la API (<see cref="LlmOptions"/>): https o red local, clave y modelos obligatorios, temperatura
/// acotada. Los mensajes de error nombran la opción o la variable, nunca su valor. Cada perfil puede tener precios en su
/// propia moneda, pero los perfiles de pago de una misma ejecución deben compartirla: el informe suma costes.
/// </summary>
public static class EvalConfigurationLoader
{
    public const string InvalidCode = "configuration.invalid";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Result<EvalConfiguration> Load(string path, IReadOnlyList<string> models, Func<string, string?> environment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(environment);

        if (!File.Exists(path))
        {
            return Invalid($"No existe el fichero de modelos {path}.");
        }

        EvalConfigurationFile? file;
        try
        {
            file = JsonSerializer.Deserialize<EvalConfigurationFile>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return Invalid($"{path} no es un JSON válido.");
        }

        return file is null ? Invalid($"{path} está vacío.") : Resolve(file, models, environment);
    }

    private static Result<EvalConfiguration> Resolve(EvalConfigurationFile file, IReadOnlyList<string> names, Func<string, string?> environment)
    {
        var profiles = new List<ModelProfile>();
        var currencies = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (!file.Models.TryGetValue(name, out var entry))
            {
                return Invalid($"El modelo '{name}' no está en el fichero de modelos.");
            }

            var profile = ToProfile(name, entry, environment);
            if (profile.IsFailure)
            {
                return Result<EvalConfiguration>.Failure(profile.Error!);
            }

            profiles.Add(profile.Value);
            if (!profile.Value.Pricing.IsFree)
            {
                currencies.Add(entry.Currency ?? file.Currency);
            }
        }

        if (currencies.Count > 1)
        {
            return Invalid(
                $"Los modelos elegidos tienen precios en monedas distintas ({string.Join(", ", currencies)}): evalúelos por separado o ponga sus precios en la misma moneda.");
        }

        var embeddings = file.Embeddings is null ? null : ToEmbeddings(file.Embeddings, environment);
        if (embeddings is { IsFailure: true })
        {
            return Result<EvalConfiguration>.Failure(embeddings.Error!);
        }

        var currency = currencies.Count == 1 ? currencies.Min! : file.Currency;
        return Result<EvalConfiguration>.Success(new EvalConfiguration(profiles.AsReadOnly(), embeddings?.Value, currency));
    }

    private static Result<ModelProfile> ToProfile(string name, ModelEntry entry, Func<string, string?> environment)
    {
        if (entry.InputPricePerMillion < 0 || entry.OutputPricePerMillion < 0)
        {
            return Result<ModelProfile>.Failure(InvalidError($"models:{name}: los precios no pueden ser negativos."));
        }

        var options = new LlmProviderOptions
        {
            Type = entry.Type,
            DisplayName = entry.DisplayName,
            Endpoint = Variable(entry.EndpointVariable, environment),
            ApiKey = Variable(entry.ApiKeyVariable, environment),
            AdapterModel = entry.AdapterModel,
            VerifierModel = entry.VerifierModel,
            Temperature = entry.Temperature,
        };
        var llm = new LlmOptions { Primary = options };
        var problems = llm.Validate(new ValidationContext(llm)).Select(problem => problem.ErrorMessage).ToList();
        if (problems.Count > 0)
        {
            var prefix = $"{LlmOptions.SectionName}:{nameof(LlmOptions.Primary)}";
            var message = string.Join(' ', problems).Replace(prefix, $"models:{name}", StringComparison.Ordinal);
            return Result<ModelProfile>.Failure(InvalidError(
                $"{message} El endpoint y la clave se leen de las variables {entry.EndpointVariable} y {entry.ApiKeyVariable}."));
        }

        return Result<ModelProfile>.Success(
            new ModelProfile(name, options, new ModelPricing(entry.InputPricePerMillion, entry.OutputPricePerMillion)));
    }

    private static Result<EmbeddingSettings> ToEmbeddings(EmbeddingEntry entry, Func<string, string?> environment)
    {
        var endpoint = Variable(entry.EndpointVariable, environment);
        var apiKey = Variable(entry.ApiKeyVariable, environment);
        if (entry.Type is not { } type || !Enum.IsDefined(type) || string.IsNullOrWhiteSpace(entry.Model)
            || string.IsNullOrWhiteSpace(apiKey) || !ProviderEndpoint.IsAllowed(endpoint))
        {
            return Result<EmbeddingSettings>.Failure(InvalidError(
                $"embeddings: hacen falta type, model y las variables {entry.EndpointVariable} (https o red local) y {entry.ApiKeyVariable}."));
        }

        return Result<EmbeddingSettings>.Success(new EmbeddingSettings(type, endpoint, apiKey, entry.Model));
    }

    private static string Variable(string name, Func<string, string?> environment) =>
        string.IsNullOrWhiteSpace(name) ? string.Empty : environment(name) ?? string.Empty;

    private static Result<EvalConfiguration> Invalid(string message) => Result<EvalConfiguration>.Failure(InvalidError(message));

    private static Error InvalidError(string message) => new(InvalidCode, message);
}
