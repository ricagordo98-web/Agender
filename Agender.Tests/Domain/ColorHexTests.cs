using Agender.Shared.Domain;
using FluentAssertions;

namespace Agender.Tests.Domain;

public sealed class ColorHexTests
{
    [Theory]
    [InlineData("#ff8800", "#FF8800")]
    [InlineData("  #AbCdEf ", "#ABCDEF")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_ValidOrEmpty_ReturnsUpperCaseOrNull(string? input, string? expected)
    {
        ColorHex.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("123456")]
    [InlineData("#1234567")]
    public void Normalize_Invalid_Throws(string input)
    {
        Action act = () => ColorHex.Normalize(input);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsValid_Null_IsValid() => ColorHex.IsValid(null).Should().BeTrue();
}
