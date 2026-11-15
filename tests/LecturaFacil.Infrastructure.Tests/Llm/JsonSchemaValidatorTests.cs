// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class JsonSchemaValidatorTests
{
    [Fact]
    public void Validate_WhenJsonMatchesTheSchema_ReturnsTheTypedValue()
    {
        var result = JsonSchemaValidator.Validate<Sample>("""{"title":"Plazo","days":15,"tags":["a"],"note":null}""");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new Sample("Plazo", 15, ["a"], null), new SampleComparer());
    }

    [Theory]
    [InlineData("""{"days":15,"tags":[]}""")]
    [InlineData("""{"title":"Plazo","tags":[]}""")]
    public void Validate_WhenARequiredFieldIsMissing_ReturnsInvalidProviderOutput(string json)
    {
        JsonSchemaValidator.Validate<Sample>(json).Error.ShouldBe(LlmErrors.InvalidProviderOutput);
    }

    [Theory]
    [InlineData("""{"title":"Plazo","days":"15","tags":[]}""")]
    [InlineData("""{"title":3,"days":15,"tags":[]}""")]
    [InlineData("""{"title":"Plazo","days":15,"tags":"a"}""")]
    [InlineData("""{"title":null,"days":15,"tags":[]}""")]
    public void Validate_WhenAFieldHasTheWrongType_ReturnsInvalidProviderOutput(string json)
    {
        JsonSchemaValidator.Validate<Sample>(json).Error.ShouldBe(LlmErrors.InvalidProviderOutput);
    }

    [Theory]
    [InlineData("""{"title":"Plazo","days":15,"tags":["a""")]
    [InlineData("""{"title":"Pla""")]
    public void Validate_WhenJsonIsTruncated_ReturnsInvalidProviderOutput(string json)
    {
        JsonSchemaValidator.Validate<Sample>(json).Error.ShouldBe(LlmErrors.InvalidProviderOutput);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("Claro, aquí tienes el JSON: {}")]
    [InlineData("```json\n{\"title\":\"Plazo\",\"days\":15,\"tags\":[]}\n```")]
    public void Validate_WhenTheResponseIsNotASingleJsonObject_ReturnsInvalidProviderOutput(string? json)
    {
        JsonSchemaValidator.Validate<Sample>(json).Error.ShouldBe(LlmErrors.InvalidProviderOutput);
    }

    [Fact]
    public void Validate_WhenThereAreUnknownFields_ReturnsInvalidProviderOutput()
    {
        var result = JsonSchemaValidator.Validate<Sample>("""{"title":"Plazo","days":15,"tags":[],"comando":"borrar"}""");

        result.Error.ShouldBe(LlmErrors.InvalidProviderOutput);
    }

    [Fact]
    public void SchemaFor_DescribesRequiredFieldsAndForbidsAdditionalProperties()
    {
        var schema = JsonSchemaValidator.SchemaFor<Sample>();

        schema.GetProperty("type").GetString().ShouldBe("object");
        schema.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
        schema.GetProperty("required").EnumerateArray().Select(item => item.GetString())
            .ShouldBe(["title", "days", "tags", "note"], ignoreOrder: true);
        schema.GetProperty("properties").GetProperty("days").GetProperty("type").GetString().ShouldBe("integer");
    }

    private sealed record Sample(string Title, int Days, IReadOnlyList<string> Tags, string? Note);

    private sealed class SampleComparer : IEqualityComparer<Sample>
    {
        public bool Equals(Sample? x, Sample? y) =>
            x is not null && y is not null && x.Title == y.Title && x.Days == y.Days && x.Note == y.Note && x.Tags.SequenceEqual(y.Tags);

        public int GetHashCode(Sample obj) => obj.Title.GetHashCode(StringComparison.Ordinal);
    }
}
