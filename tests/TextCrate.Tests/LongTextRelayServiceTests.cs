using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace TextCrate.Tests;

public sealed class LongTextRelayServiceTests
{
    [Fact]
    public void ShouldOfferReturnsFalseWhenRelayDisabled()
    {
        var settings = new AppSettings
        {
            LongTextRelayEnabled = false,
            LongTextRelayEndpoint = "https://example.invalid",
            LongTextRelayOfferOver = 1000
        };

        Assert.False(LongTextRelayService.ShouldOffer(new string('a', 2000), settings));
    }

    [Fact]
    public void ShouldOfferReturnsTrueForLongConfiguredText()
    {
        var settings = new AppSettings
        {
            LongTextRelayEnabled = true,
            LongTextRelayEndpoint = "https://example.invalid",
            LongTextRelayOfferOver = 1000
        };

        Assert.True(LongTextRelayService.ShouldOffer(new string('a', 1000), settings));
    }

    [Fact]
    public void GetEndpointUsesBuiltInEndpointUnlessCustomIsEnabled()
    {
        var settings = new AppSettings
        {
            LongTextRelayUseCustomEndpoint = false,
            LongTextRelayEndpoint = "https://custom.example"
        };

        Assert.Equal(AppSettings.DefaultLongTextRelayEndpoint, LongTextRelayService.GetEndpoint(settings));
    }

    [Fact]
    public void GetEndpointUsesCustomEndpointWhenEnabled()
    {
        var settings = new AppSettings
        {
            LongTextRelayUseCustomEndpoint = true,
            LongTextRelayEndpoint = " https://custom.example "
        };

        Assert.Equal("https://custom.example", LongTextRelayService.GetEndpoint(settings));
    }

    [Fact]
    public void PasswordDerivationIsDeterministicForSameInputs()
    {
        var masterKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var salt = Enumerable.Range(32, 16).Select(i => (byte)i).ToArray();

        var first = LongTextRelayService.DeriveContentKey(masterKey, salt, "correct horse battery staple");
        var second = LongTextRelayService.DeriveContentKey(masterKey, salt, "correct horse battery staple");

        Assert.Equal(first, second);
    }

    [Fact]
    public void PasswordDerivationChangesWithPassword()
    {
        var masterKey = RandomNumberGenerator.GetBytes(32);
        var salt = RandomNumberGenerator.GetBytes(16);

        var first = LongTextRelayService.DeriveContentKey(masterKey, salt, "one");
        var second = LongTextRelayService.DeriveContentKey(masterKey, salt, "two");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Base64UrlRoundTripsOpaqueTokens()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        var encoded = Base64Url.Encode(bytes);
        var decoded = Base64Url.Decode(encoded);

        Assert.DoesNotContain("+", encoded);
        Assert.DoesNotContain("/", encoded);
        Assert.DoesNotContain("=", encoded);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void BurnTokenHashDerivationIsDeterministicForSameKey()
    {
        var key = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

        var first = LongTextRelayService.DeriveBurnTokenHash(key);
        var second = LongTextRelayService.DeriveBurnTokenHash(key);

        Assert.Equal(first, second);
    }
}
