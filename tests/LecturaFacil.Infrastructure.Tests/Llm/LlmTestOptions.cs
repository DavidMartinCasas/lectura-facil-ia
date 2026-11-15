// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Infrastructure.Tests.Llm;

internal static class LlmTestOptions
{
    public const string ApiKey = "clave-de-prueba-que-no-debe-salir";

    public static LlmProviderOptions Provider(LlmProviderType type, string endpoint) => new()
    {
        Type = type,
        DisplayName = type == LlmProviderType.AzureFoundry ? "Nube de pruebas" : "Servidor de pruebas",
        Endpoint = endpoint,
        ApiKey = ApiKey,
        AdapterModel = "despliegue-adaptador",
        VerifierModel = "despliegue-verificador",
    };
}
