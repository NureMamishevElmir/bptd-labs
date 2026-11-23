using System.Numerics;
using System.Security.Cryptography;
using Rsa.Core;
using Xunit;

namespace Rsa.Tests;

public class NumberTheoryTests
{
    private static BigInteger RandomBig(int bytes) =>
        new(RandomNumberGenerator.GetBytes(bytes), isUnsigned: true, isBigEndian: true);

    [Fact]
    public void ModPow_MatchesBigIntegerModPow_OnRandomInputs()
    {
        // BigInteger.ModPow використовується лише тут, як еталон
        for (int i = 0; i < 300; i++)
        {
            var b = RandomBig(1 + i % 64);
            var e = RandomBig(1 + i % 32);
            var m = RandomBig(1 + i % 48) + 2;
            Assert.Equal(BigInteger.ModPow(b, e, m), NumberTheory.ModPow(b, e, m));
        }
    }

    [Theory]
    [InlineData(2, 10, 1000, 24)]
    [InlineData(65, 17, 3233, 2790)]
    [InlineData(2790, 2753, 3233, 65)]
    [InlineData(5, 0, 7, 1)]
    [InlineData(0, 5, 7, 0)]
    public void ModPow_KnownValues(int b, int e, int m, int expected) =>
        Assert.Equal(expected, (int)NumberTheory.ModPow(b, e, m));

    [Theory]
    [InlineData(48, 18, 6)]
    [InlineData(17, 3120, 1)]
    [InlineData(0, 5, 5)]
    [InlineData(270, 192, 6)]
    public void Gcd_KnownValues(int a, int b, int expected) =>
        Assert.Equal(expected, (int)NumberTheory.Gcd(a, b));

    [Fact]
    public void ExtendedGcd_SatisfiesBezoutIdentity()
    {
        for (int i = 0; i < 100; i++)
        {
            var a = RandomBig(32) + 1;
            var b = RandomBig(24) + 1;
            var (g, x, y) = NumberTheory.ExtendedGcd(a, b);
            Assert.Equal(g, a * x + b * y);
            Assert.Equal(BigInteger.GreatestCommonDivisor(a, b), g);
        }
    }

    [Fact]
    public void ModInverse_TextbookAndRandom()
    {
        Assert.Equal(2753, (int)NumberTheory.ModInverse(17, 3120));
        var m = PrimeGenerator.GeneratePrime(128);
        for (int i = 0; i < 50; i++)
        {
            var a = PrimeGenerator.RandomInRange(1, m - 1);
            Assert.Equal(BigInteger.One, a * NumberTheory.ModInverse(a, m) % m);
        }
    }

    [Fact]
    public void ModInverse_Throws_WhenNotCoprime() =>
        Assert.Throws<ArgumentException>(() => NumberTheory.ModInverse(6, 9));

    [Fact]
    public void Hex_RoundTrip()
    {
        for (int i = 0; i < 50; i++)
        {
            var x = RandomBig(1 + i);
            Assert.Equal(x, Hex.FromHex(Hex.ToHex(x)));
        }
        Assert.Equal("CA1", Hex.ToHex(3233));
        Assert.Equal("0", Hex.ToHex(0));
    }
}
