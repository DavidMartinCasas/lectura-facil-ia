// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Evals.Configuration;
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Evals.Tests.Configuration;

public sealed class EvalConfigurationLoaderTests : IDisposable
{
    private const string Config = """
        {
          "currency": "USD",
          "embeddings": { "type": "OpenAICompatible", "endpointVariable": "EMB_ENDPOINT", "apiKeyVariable": "EMB_KEY", "model": "bge-m3" },
          "models": {
            "local": {
              "type": "OpenAICompatible", "displayName": "Servidor propio",
              "endpointVariable": "LOCAL_ENDPOINT", "apiKeyVariable": "LOCAL_KEY",
              "adapterModel": "adaptador", "verifierModel": "verificador"
            },
            "nube": {
              "type": "AzureFoundry", "displayName": "Nube",
              "endpointVariable": "NUBE_ENDPOINT", "apiKeyVariable": "NUBE_KEY",
              "adapterModel": "mini", "verifierModel": "otro", "temperature": 0.2,
              "inputPricePerMillion": 0.44, "outputPricePerMillion": 1.76
            },
            "europa": {
              "type": "OpenAICompatible", "displayName": "Nube UE",
              "endpointVariable": "NUBE_ENDPOINT", "apiKeyVariable": "NUBE_KEY",
              "adapterModel": "grande", "verifierModel": "pequeño", "currency": "EUR",
              "inputPricePerMillion": 0.09, "outputPricePerMillion": 0.35
            },
            "negativo": {
              "type": "OpenAICompatible", "displayName": "Mal", "endpointVariable": "LOCAL_ENDPOINT", "apiKeyVariable": "LOCAL_KEY",
              "adapterModel": "a", "verifierModel": "b", "inputPricePerMillion": -1
            }
          }
        }
        """;

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"evals-modelos-{Guid.NewGuid():N}.json");

    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal)
    {
        ["LOCAL_ENDPOINT"] = "http://localhost:8080/v1",
        ["LOCAL_KEY"] = "clave-de-prueba",
        ["NUBE_ENDPOINT"] = "https://recurso.example/",
        ["NUBE_KEY"] = "clave-de-prueba",
        ["EMB_ENDPOINT"] = "http://127.0.0.1:8081/v1",
        ["EMB_KEY"] = "clave-de-prueba",
    };

    public EvalConfigurationLoaderTests() => File.WriteAllText(_path, Config);

    [Fact]
    public void Load_WhenProfilesAreValid_ResolvesThemWithTheirEnvironmentAndPricing()
    {
        var result = Load("local", "nube");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Currency.ShouldBe("USD");
        result.Value.Models.Select(model => model.Name).ShouldBe(["local", "nube"]);
        var nube = result.Value.Models[1];
        nube.Provider.Type.ShouldBe(LlmProviderType.AzureFoundry);
        nube.Provider.Endpoint.ShouldBe("https://recurso.example/");
        nube.Provider.Temperature.ShouldBe(0.2);
        nube.Pricing.InputPerMillion.ShouldBe(0.44m);
        result.Value.Models[0].Pricing.IsFree.ShouldBeTrue();
        result.Value.Embeddings!.Model.ShouldBe("bge-m3");
    }

    [Fact]
    public void Load_WhenAPaidProfileHasItsOwnCurrency_UsesItForTheRun()
    {
        var result = Load("local", "europa");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Currency.ShouldBe("EUR");
        result.Value.Models[1].Pricing.OutputPerMillion.ShouldBe(0.35m);
    }

    [Fact]
    public void Load_WhenPaidProfilesUseDifferentCurrencies_FailsNamingThem()
    {
        var result = Load("nube", "europa");

        result.Error!.Code.ShouldBe(EvalConfigurationLoader.InvalidCode);
        result.Error.Message.ShouldContain("EUR, USD");
    }

    [Fact]
    public void Load_WhenAProfileIsNotInTheFile_Fails()
    {
        Load("desconocido").Error!.Message.ShouldContain("desconocido");
    }

    [Fact]
    public void Load_WhenTheKeyVariableIsMissing_FailsNamingTheVariableButNotAnyValue()
    {
        _environment.Remove("NUBE_KEY");

        var result = Load("nube");

        result.Error!.Code.ShouldBe(EvalConfigurationLoader.InvalidCode);
        result.Error.Message.ShouldContain("NUBE_KEY");
        result.Error.Message.ShouldContain("models:nube:ApiKey");
        result.Error.Message.ShouldNotContain("recurso.example");
    }

    [Fact]
    public void Load_WhenTheEndpointIsPlainHttpToAPublicHost_Fails()
    {
        _environment["LOCAL_ENDPOINT"] = "http://servidor.example/v1";

        Load("local").Error!.Message.ShouldContain("models:local:Endpoint");
    }

    [Fact]
    public void Load_WhenPricesAreNegative_Fails()
    {
        Load("negativo").Error!.Code.ShouldBe(EvalConfigurationLoader.InvalidCode);
    }

    [Fact]
    public void Load_WhenEmbeddingsAreMisconfigured_Fails()
    {
        _environment.Remove("EMB_KEY");

        Load("local").Error!.Message.ShouldContain("EMB_KEY");
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_Fails()
    {
        EvalConfigurationLoader.Load(_path + ".no", ["local"], _environment.GetValueOrDefault).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Load_WhenFileIsNotJson_Fails()
    {
        File.WriteAllText(_path, "{ no es json");

        Load("local").Error!.Code.ShouldBe(EvalConfigurationLoader.InvalidCode);
    }

    [Fact]
    public void ToString_OfEmbeddings_DoesNotIncludeTheKey()
    {
        var settings = new EmbeddingSettings(LlmProviderType.OpenAICompatible, "http://localhost/v1", "clave-secreta", "bge-m3");

        settings.ToString().ShouldNotContain("clave-secreta");
    }

    public void Dispose() => File.Delete(_path);

    private Result<EvalConfiguration> Load(params string[] models) =>
        EvalConfigurationLoader.Load(_path, models, _environment.GetValueOrDefault);
}
