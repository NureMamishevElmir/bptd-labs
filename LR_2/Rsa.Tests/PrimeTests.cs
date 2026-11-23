using System.Numerics;
using Rsa.Core;
using Xunit;

namespace Rsa.Tests;

public class PrimeTests
{
    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("61")]
    [InlineData("53")]
    [InlineData("7919")]
    [InlineData("65537")]
    [InlineData("1000003")]
    [InlineData("2305843009213693951")]                          // 2^61 − 1
    [InlineData("170141183460469231731687303715884105727")]      // 2^127 − 1
    public void MillerRabin_RecognizesPrimes(string n) =>
        Assert.True(PrimeGenerator.IsProbablePrime(BigInteger.Parse(n)));

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("4")]
    [InlineData("3233")]                                         // 61·53
    [InlineData("561")]                                          // числа Кармайкла
    [InlineData("1105")]
    [InlineData("1729")]
    [InlineData("2465")]
    [InlineData("6601")]
    [InlineData("41041")]
    [InlineData("3215031751")]                                   // сильне псевдопросте за основами 2,3,5,7
    [InlineData("4951760154835678088235319297")]                 // (2^61 − 1)(2^31 − 1)
    public void MillerRabin_RejectsComposites(string n) =>
        Assert.False(PrimeGenerator.IsProbablePrime(BigInteger.Parse(n)));

    [Fact]
    public void MillerRabin_AgreesWithTrialDivision_UpTo5000()
    {
        for (int n = 0; n <= 5000; n++)
        {
            bool expected = n >= 2;
            for (int d = 2; d * d <= n && expected; d++)
                if (n % d == 0) expected = false;
            Assert.Equal(expected, PrimeGenerator.IsProbablePrime(n));
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(512)]
    public void GeneratePrime_HasExactBitLength(int bits)
    {
        var p = PrimeGenerator.GeneratePrime(bits);
        Assert.Equal(bits, NumberTheory.BitLength(p));
        Assert.False(p.IsEven);
        Assert.True(PrimeGenerator.IsProbablePrime(p));
    }

    [Fact]
    public void RandomInRange_StaysInBounds()
    {
        for (int i = 0; i < 500; i++)
        {
            var r = PrimeGenerator.RandomInRange(10, 20);
            Assert.InRange(r, 10, 20);
        }
    }
}
