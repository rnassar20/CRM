using Crm.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Crm.Api.UnitTests;

public class LicenseKeyServiceTests
{
    private const string Secret = "baaf945deabc60b9706001bce0898126534aac3bf64b184d";

    private static LicenseKeyService CreateService()
        => new(MakeConfig());

    private static IConfiguration MakeConfig()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:Secret"] = Secret
            })
            .Build();

    [Fact]
    public void GenerateAndParse_RoundTrips_Facts()
    {
        var svc = CreateService();
        var expiry = new DateTime(2027, 3, 15);

        var key = svc.GenerateKey(42, 7, expiry);
        var (clientId, subId, parsedExpiry) = svc.ParseKey(key);

        clientId.Should().Be(42);
        subId.Should().Be(7);
        parsedExpiry.Should().Be(expiry.Date);
    }

    [Fact]
    public void GenerateKey_Is_Deterministic_AcrossCalls()
    {
        // Each key embeds a random IV, so identical inputs must NOT produce identical keys
        // (an IV is generated per call by AES-CBC). This guards against a regression to
        // a fixed/zero IV, which would allow duplicate-key forgery.
        var svc = CreateService();
        var a = svc.GenerateKey(1, 1, new DateTime(2027, 1, 1));
        var b = svc.GenerateKey(1, 1, new DateTime(2027, 1, 1));
        a.Should().NotBe(b);
    }

    [Fact]
    public void ParseKey_TamperedCharacter_Throws()
    {
        var svc = CreateService();
        var key = svc.GenerateKey(5, 9, new DateTime(2026, 12, 1));

        // Flip a character in the payload section (after the "ROM" prefix) without touching the MAC.
        var corrupted = key[..(key.Length - 5)] +
                        (key[key.Length - 5] == 'A' ? 'B' : 'A') +
                        key[(key.Length - 4)..];

        var act = () => svc.ParseKey(corrupted);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*License key integrity check failed*");
    }

    [Fact]
    public void ParseKey_GarbledInput_Throws()
    {
        var svc = CreateService();
        var act = () => svc.ParseKey("THIS-IS-NOT-A-VALID-LICENSE-KEY-1234");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseKey_EmptyKey_Throws()
    {
        var svc = CreateService();
        var act = () => svc.ParseKey("");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseKey_KeyTooShort_Throws()
    {
        var svc = CreateService();
        var act = () => svc.ParseKey("ABCD");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseKey_DecoratedKey_IsNormalised_AndAccepted()
    {
        var svc = CreateService();
        var key = svc.GenerateKey(3, 4, new DateTime(2026, 10, 10));

        // Lowercase + spaces + missing group separators must still decode after normalisation.
        var decorated = key.ToLowerInvariant().Replace("-", "");
        var (clientId, subId, expiry) = svc.ParseKey(decorated);

        clientId.Should().Be(3);
        subId.Should().Be(4);
        expiry.Should().Be(new DateTime(2026, 10, 10));
    }

    [Fact]
    public void HashKey_IsStable_And_CaseInsensitive()
    {
        var svc = CreateService();
        var key = svc.GenerateKey(2, 2, new DateTime(2026, 9, 1));

        var h1 = svc.HashKey(key);
        var h2 = svc.HashKey(key.ToLowerInvariant().Replace("-", ""));

        h1.Should().Be(h2);
        h1.Should().MatchRegex("^[0-9A-F]{64}$"); // SHA-256 hex
    }

    [Fact]
    public void DifferentSecrets_ProduceDifferentKeys()
    {
        var svcA = new LicenseKeyService(MakeConfig());
        var svcB = new LicenseKeyService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Licensing:Secret"] = "another-secret-value-0123456789abcdef"
            })
            .Build());

        var keyA = svcA.GenerateKey(10, 10, new DateTime(2027, 5, 5));
        var keyB = svcB.GenerateKey(10, 10, new DateTime(2027, 5, 5));

        keyA.Should().NotBe(keyB);
    }
}
