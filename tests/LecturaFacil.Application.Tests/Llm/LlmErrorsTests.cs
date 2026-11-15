// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Application.Tests.Llm;

public sealed class LlmErrorsTests
{
    [Fact]
    public void Errors_HaveStableCodesAndMessagesWithoutInternalDetails()
    {
        LlmErrors.InvalidProviderOutput.Code.ShouldBe("llm.invalid_provider_output");
        LlmErrors.ProviderUnavailable.Code.ShouldBe("llm.provider_unavailable");
        LlmErrors.InputTooLong.Code.ShouldBe("llm.input_too_long");
        LlmErrors.InputTooLong.Message.ShouldNotBeNullOrWhiteSpace();
        LlmErrors.InvalidProviderOutput.Message.ShouldNotBeNullOrWhiteSpace();
        LlmErrors.ProviderUnavailable.Message.ShouldNotBeNullOrWhiteSpace();
    }
}
