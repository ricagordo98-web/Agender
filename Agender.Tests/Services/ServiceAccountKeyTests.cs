using Agender.Infrastructure.Google;
using FluentAssertions;

namespace Agender.Tests.Services;

public class ServiceAccountKeyTests
{
    [Fact]
    public void Parse_ValidServiceAccountKey_ReturnsEmailAndPrivateKey()
    {
        ServiceAccountKey key = ServiceAccountKey.Parse("{\"type\":\"service_account\",\"client_email\":\"bot@p.iam.gserviceaccount.com\",\"private_key\":\"abc\"}");

        key.Should().Be(new ServiceAccountKey("bot@p.iam.gserviceaccount.com", "abc"));
    }

    [Fact]
    public void Parse_IgnoresATokenUriSoTheServerNeverCallsAUserSuppliedUrl()
    {
        ServiceAccountKey key = ServiceAccountKey.Parse(
            "{\"type\":\"service_account\",\"client_email\":\"bot@p.com\",\"private_key\":\"abc\",\"token_uri\":\"http://169.254.169.254/\"}");

        key.ClientEmail.Should().Be("bot@p.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"type\":\"authorized_user\",\"client_email\":\"a@b.com\",\"private_key\":\"k\"}")]
    [InlineData("{\"type\":\"service_account\",\"private_key\":\"k\"}")]
    [InlineData("{\"type\":\"service_account\",\"client_email\":\"a@b.com\"}")]
    [InlineData("{\"type\":\"service_account\",\"client_email\":\"\",\"private_key\":\"k\"}")]
    public void Parse_InvalidKey_ThrowsArgumentException(string json)
    {
        Action act = () => ServiceAccountKey.Parse(json);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_TooLarge_Throws()
    {
        Action act = () => ServiceAccountKey.Parse(new string(' ', ServiceAccountKey.MaxJsonLength + 1));

        act.Should().Throw<ArgumentException>();
    }
}
