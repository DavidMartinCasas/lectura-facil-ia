// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Api.Tests;

/// <summary>Configuración mínima válida para arrancar la API en los tests. Sin secretos reales.</summary>
internal static class TestConfiguration
{
    public const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=2";

    public static readonly IReadOnlyDictionary<string, string> ValidLlmSettings = new Dictionary<string, string>
    {
        ["Llm:Primary:Type"] = "OpenAICompatible",
        ["Llm:Primary:DisplayName"] = "Servidor de pruebas",
        ["Llm:Primary:Endpoint"] = "http://localhost:11434/v1",
        ["Llm:Primary:ApiKey"] = "clave-de-prueba",
        ["Llm:Primary:AdapterModel"] = "modelo-adaptador",
        ["Llm:Primary:VerifierModel"] = "modelo-verificador",
    };
}
