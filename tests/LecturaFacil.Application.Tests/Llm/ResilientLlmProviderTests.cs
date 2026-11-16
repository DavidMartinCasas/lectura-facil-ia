// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Application.Tests.Llm;

public sealed class ResilientLlmProviderTests
{
    private static readonly LlmRequest Request = new(LlmRole.Adapter, "sistema", "texto del documento", 100);

    [Fact]
    public async Task Complete_WhenPrimaryResponds_ReturnsThePrimaryResponseAndNeverCallsTheFallback()
    {
        var primary = StubLlmProvider.Responding("Principal");
        var fallback = StubLlmProvider.Responding("Respaldo");
        var provider = new ResilientLlmProvider(primary, fallback, fallbackEnabled: true, Availability(primary: true));

        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Value.Provider.ShouldBe("Principal");
        primary.Calls.ShouldBe(1);
        fallback.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Complete_WhenPrimaryIsUnavailableAndFallbackIsEnabled_ReturnsTheFallbackResponseNamingIt()
    {
        var primary = StubLlmProvider.Unavailable("Principal");
        var fallback = StubLlmProvider.Responding("Respaldo");
        var provider = new ResilientLlmProvider(primary, fallback, fallbackEnabled: true, Availability(primary: true));

        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Value.Provider.ShouldBe("Respaldo");
        primary.Calls.ShouldBe(1);
        fallback.Requests.ShouldHaveSingleItem().ShouldBeSameAs(Request);
    }

    [Fact]
    public async Task Complete_WhenPrimaryIsUnavailableAndFallbackIsDisabled_NeverCallsTheFallback()
    {
        var primary = StubLlmProvider.Unavailable("Principal");
        var fallback = StubLlmProvider.Responding("Respaldo");
        var provider = new ResilientLlmProvider(primary, fallback, fallbackEnabled: false, Availability(primary: false));

        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(LlmErrors.ProviderUnavailable);
        fallback.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Complete_WhenFallbackIsDisabledAndHealthCheckIsRed_StillTriesThePrimary()
    {
        var primary = StubLlmProvider.Responding("Principal");
        var provider = new ResilientLlmProvider(primary, null, fallbackEnabled: false, Availability(primary: false));

        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Value.Provider.ShouldBe("Principal");
    }

    [Fact]
    public async Task Complete_WhenFallbackIsEnabledButNotConfigured_ReturnsProviderUnavailable()
    {
        var primary = StubLlmProvider.Unavailable("Principal");
        var provider = new ResilientLlmProvider(primary, null, fallbackEnabled: true, Availability(primary: true));

        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(LlmErrors.ProviderUnavailable);
    }

    [Fact]
    public async Task Complete_WhenHealthCheckSaysPrimaryIsDownAndFallbackIsEnabled_GoesStraightToTheFallback()
    {
        var primary = StubLlmProvider.Responding("Principal");
        var fallback = StubLlmProvider.Responding("Respaldo");
        var provider = new ResilientLlmProvider(primary, fallback, fallbackEnabled: true, Availability(primary: false));

        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Value.Provider.ShouldBe("Respaldo");
        primary.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Complete_WhenHealthCheckRecovers_GoesBackToThePrimary()
    {
        var primary = StubLlmProvider.Responding("Principal");
        var fallback = StubLlmProvider.Responding("Respaldo");
        var availability = Availability(primary: false);
        var provider = new ResilientLlmProvider(primary, fallback, fallbackEnabled: true, availability);
        await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        availability.Current = new LlmAvailabilityStatus(PrimaryAvailable: true, FallbackAvailable: true);
        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Value.Provider.ShouldBe("Principal");
        fallback.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Complete_WhenPrimaryReturnsInvalidOutput_DoesNotUseTheFallback()
    {
        var primary = StubLlmProvider.Failing("Principal", LlmErrors.InvalidProviderOutput);
        var fallback = StubLlmProvider.Responding("Respaldo");
        var provider = new ResilientLlmProvider(primary, fallback, fallbackEnabled: true, Availability(primary: true));

        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(LlmErrors.InvalidProviderOutput);
        fallback.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Complete_WhenBothProvidersAreUnavailable_ReturnsProviderUnavailable()
    {
        var provider = new ResilientLlmProvider(
            StubLlmProvider.Unavailable("Principal"),
            StubLlmProvider.Unavailable("Respaldo"),
            fallbackEnabled: true,
            Availability(primary: true));

        var result = await provider.CompleteStructuredAsync<Reply>(Request, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(LlmErrors.ProviderUnavailable);
    }

    [Fact]
    public void Name_IsThePrimaryName()
    {
        var provider = new ResilientLlmProvider(
            StubLlmProvider.Responding("Principal"),
            null,
            fallbackEnabled: false,
            Availability(primary: true));

        provider.Name.ShouldBe("Principal");
    }

    private static StubAvailability Availability(bool primary) => new() { Current = new(primary, null) };

    internal sealed record Reply(bool Ok);

    private sealed class StubAvailability : ILlmAvailability
    {
        public LlmAvailabilityStatus Current { get; set; } = new(true, null);
    }

    /// <summary>Proveedor de prueba que guarda cada petición y devuelve siempre el mismo resultado.</summary>
    private sealed class StubLlmProvider(string name, Error? error) : ILlmProvider
    {
        private readonly List<LlmRequest> _requests = [];

        public string Name { get; } = name;

        public IReadOnlyList<LlmRequest> Requests => _requests;

        public int Calls => _requests.Count;

        public static StubLlmProvider Responding(string name) => new(name, null);

        public static StubLlmProvider Unavailable(string name) => new(name, LlmErrors.ProviderUnavailable);

        public static StubLlmProvider Failing(string name, Error error) => new(name, error);

        public Task<Result<LlmResponse<T>>> CompleteStructuredAsync<T>(LlmRequest request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            if (error is not null)
            {
                return Task.FromResult(Result<LlmResponse<T>>.Failure(error));
            }

            var value = (T)(object)new Reply(true);
            return Task.FromResult(Result<LlmResponse<T>>.Success(
                new LlmResponse<T>(value, Name, "modelo", 10, 2, TimeSpan.FromMilliseconds(5))));
        }
    }
}
