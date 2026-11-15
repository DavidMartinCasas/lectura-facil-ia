// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Tests.Common;

public sealed class ResultTests
{
    private static readonly Error SampleError = new("document.not_found", "Documento no encontrado.");

    [Fact]
    public void Success_WhenCreated_IsSuccessWithoutError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_WhenCreated_IsFailureWithError()
    {
        var result = Result.Failure(SampleError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
    }

    [Fact]
    public void GenericSuccess_WhenCreated_ExposesValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void GenericFailure_WhenValueIsRead_Throws()
    {
        var result = Result<int>.Failure(SampleError);

        result.Error.ShouldBe(SampleError);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_WhenErrorIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
    }
}
