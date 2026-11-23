using System.Numerics;
using Dh.Core;

namespace Dh.Tests;

public class DhTests
{
    [Fact] // власний ModPow збігається з BigInteger.ModPow
    public void ModPow_MatchesBigInteger()
    {
        for (int i = 0; i < 200; i++)
        {
            BigInteger m = ModMath.RandomBelow(BigInteger.One << 512) + 2;
            BigInteger a = ModMath.RandomBelow(BigInteger.One << 600);
            BigInteger e = ModMath.RandomBelow(BigInteger.One << 300);
            Assert.Equal(BigInteger.ModPow(a, e, m), ModMath.ModPow(a, e, m));
        }
        Assert.Equal(1, ModMath.ModPow(7, 0, 13));
        Assert.Equal(0, ModMath.ModPow(7, 5, 1));
    }

    [Fact] // приклад з методички: Q=23, A=5, XB=6, XC=15
    public void SmallExample_TwoUsers()
    {
        var b = new DhParty("B", DhGroup.Small, 6);
        var c = new DhParty("C", DhGroup.Small, 15);
        Assert.Equal(8, b.Y);
        Assert.Equal(19, c.Y);
        Assert.Equal(2, b.SharedKey(c));
        Assert.Equal(2, c.SharedKey(b));
    }

    [Fact]
    public void PrimitiveRoot_Small()
    {
        Assert.True(ModMath.IsPrimitiveRootBrute(5, 23));
        Assert.False(ModMath.IsPrimitiveRootBrute(2, 23));
        // 23 = 2*11+1 — безпечне просте, швидка перевірка дає той самий результат
        Assert.True(ModMath.IsPrimitiveRootSafePrime(5, 23));
        Assert.False(ModMath.IsPrimitiveRootSafePrime(2, 23));
        Assert.Equal(5, ModMath.SmallestPrimitiveRootSafePrime(23));
    }

    [Fact] // група RFC 3526: Q безпечне просте, обраний A — первісний корінь
    public void Rfc3526_GroupIsValid()
    {
        var g = DhGroup.Rfc3526;
        Assert.Equal(2048, g.Bits);
        Assert.True(ModMath.IsProbablePrime(g.Q));
        Assert.True(ModMath.IsProbablePrime((g.Q - 1) / 2));
        Assert.Equal(11, g.A);
        Assert.True(ModMath.IsPrimitiveRootSafePrime(g.A, g.Q));
        Assert.False(ModMath.IsPrimitiveRootSafePrime(2, g.Q)); // 2 породжує лише підгрупу порядку q
    }

    [Theory] // троє учасників: попарні ключі симетричні
    [InlineData(false)]
    [InlineData(true)]
    public void ThreeParties_PairwiseKeysSymmetric(bool bigGroup)
    {
        var grp = bigGroup ? DhGroup.Rfc3526 : DhGroup.Small;
        var u = new[] { new DhParty("B", grp), new DhParty("C", grp), new DhParty("D", grp) };
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (i != j)
                    Assert.Equal(u[i].SharedKey(u[j]), u[j].SharedKey(u[i]));
        if (bigGroup) // ключі різних пар різні
            Assert.NotEqual(u[0].SharedKey(u[1]), u[0].SharedKey(u[2]));
    }

    [Fact]
    public void SharedKey_RejectsTrivialY()
    {
        var b = new DhParty("B", DhGroup.Small, 6);
        Assert.Throws<ArgumentException>(() => b.SharedKey(1));
        Assert.Throws<ArgumentException>(() => b.SharedKey(22));
    }

    [Fact] // відбиток = перші 8 hex SHA-256 від байтів K
    public void Fingerprint_Format()
    {
        Assert.Equal("dbc1b4c9", DhParty.Fingerprint(2)); // SHA-256([0x02])
        Assert.Matches("^[0-9a-f]{8}$", DhParty.Fingerprint(DhGroup.Rfc3526.Q - 5));
    }
}
