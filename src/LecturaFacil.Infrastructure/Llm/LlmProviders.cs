// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Proveedores configurados. El respaldo solo existe si está habilitado: si no, ningún documento sale del principal.
/// </summary>
public sealed class LlmProviders
{
    public LlmProviders(ILlmProvider primary, ILlmProvider? fallback)
    {
        ArgumentNullException.ThrowIfNull(primary);
        Primary = primary;
        Fallback = fallback;
    }

    public ILlmProvider Primary { get; }

    public ILlmProvider? Fallback { get; }

    public IReadOnlyList<ILlmProvider> All => Fallback is null ? [Primary] : [Primary, Fallback];

    public static LlmProviders Create(IOptions<LlmOptions> options, LlmProviderFactory factory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(factory);

        var llm = options.Value;
        var fallback = llm.Fallback.Enabled
            ? factory.Create(llm.Fallback, LlmProviderFactory.FallbackHttpClient)
            : null;
        return new LlmProviders(factory.Create(llm.Primary, LlmProviderFactory.PrimaryHttpClient), fallback);
    }
}
