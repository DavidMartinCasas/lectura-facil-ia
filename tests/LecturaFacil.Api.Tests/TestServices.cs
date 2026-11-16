// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Adaptation;
using LecturaFacil.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LecturaFacil.Api.Tests;

internal static class TestServices
{
    /// <summary>Los tests nunca llaman a un modelo real: el adaptador y el verificador responden con grabaciones.</summary>
    public static IServiceCollection UseFakeLlm(this IServiceCollection services, ILlmProvider? llm = null)
    {
        var provider = llm ?? ExampleLlm();
        services.AddTransient(_ => provider);
        return services;
    }

    /// <summary>Proveedor falso que responde a cada rol con su grabación del ejemplo del IPREM.</summary>
    public static FakeLlmProvider ExampleLlm() =>
        new("Proveedor falso", request => LlmRecording.Load(request.Role == LlmRole.Verifier ? "verify/ok" : "adapter/ok"));

    /// <summary>Sin el worker de adaptación, para que los documentos se queden en el estado que prepara cada test.</summary>
    public static IServiceCollection RemoveAdaptationWorker(this IServiceCollection services)
    {
        var worker = services.Single(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(AdaptationWorker));
        services.Remove(worker);
        return services;
    }
}
