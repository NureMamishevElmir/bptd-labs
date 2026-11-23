using System.Numerics;
using Rsa.Core;
using Xunit;

namespace Rsa.Tests;

public class RsaTests
{
    private const string UkrainianText =
        "Привіт, світе! Безпека програм та даних — лабораторна №2 (RSA). Ґанок, їжак, єнот. 🔐 ✓";

    [Fact]
    public void Textbook_P61_Q53_E17()
    {
        var k = RsaKeyPair.FromPrimes(61, 53, 17);
        Assert.Equal(3233, (int)k.N);
        Assert.Equal(3120, (int)k.Phi);
        Assert.Equal(2753, (int)k.D);
        Assert.Equal(2790, (int)RsaCipher.EncryptBlock(65, k.Public));
        Assert.Equal(65, (int)RsaCipher.DecryptBlock(2790, k.Private));
    }

    [Fact]
    public void SmallKey_RoundTripsText_ByteByByte()
    {
        var k = RsaKeyPair.FromPrimes(61, 53, 17);           // k = 2 байти → блок 1 байт
        Assert.Equal(1, RsaCipher.BlockBytes(k.N));
        var c = RsaCipher.Encrypt("Привіт", k.Public);
        Assert.Equal(12, c.Count);                           // 6 кириличних літер × 2 байти
        Assert.Equal("Привіт", RsaCipher.Decrypt(c, k.Private));
    }

    [Theory]
    [InlineData(512, 65537)]
    [InlineData(1024, 65537)]
    [InlineData(2048, 65537)]
    [InlineData(512, 17)]
    [InlineData(1024, 257)]
    public void GeneratedKeys_AreValid_AndRoundTripUkrainianText(int bits, int e)
    {
        var k = RsaKeyPair.Generate(bits, e);

        Assert.Equal(bits, k.Bits);
        Assert.Equal(k.P * k.Q, k.N);
        Assert.Equal((k.P - 1) * (k.Q - 1), k.Phi);
        Assert.Equal(BigInteger.One, k.D * k.E % k.Phi);
        Assert.Equal(BigInteger.One, NumberTheory.Gcd(k.E, k.Phi));

        var blocks = RsaCipher.TextToBlocks(UkrainianText, k.N);
        Assert.All(blocks, m => Assert.True(m < k.N));

        var cipher = RsaCipher.Encrypt(UkrainianText, k.Public);
        Assert.Equal(blocks.Count, cipher.Count);
        Assert.Equal(UkrainianText, RsaCipher.Decrypt(cipher, k.Private));
    }

    [Fact]
    public void LongText_UsesMultipleBlocks_AndRoundTrips()
    {
        var k = RsaKeyPair.Generate(512);
        var text = string.Concat(Enumerable.Repeat(UkrainianText, 10));
        var cipher = RsaCipher.Encrypt(text, k.Public);
        Assert.True(cipher.Count > 5);
        Assert.Equal(text, RsaCipher.Decrypt(cipher, k.Private));
    }

    [Fact]
    public void BlocksWithLeadingZeroBytes_AreRestored()
    {
        var k = RsaKeyPair.Generate(512);
        // 512 біт → блок 63 байти; другий (не останній) блок складається з одних нулів
        var text = "A" + new string('x', 62) + new string('\0', 63) + "Б";
        Assert.Equal(text, RsaCipher.Decrypt(RsaCipher.Encrypt(text, k.Public), k.Private));
    }

    [Fact]
    public void WrongPrivateKey_DoesNotRecoverText()
    {
        var a = RsaKeyPair.Generate(512);
        var b = RsaKeyPair.Generate(512);
        var cipher = RsaCipher.Encrypt("Таємне повідомлення", a.Public);
        var wrong = cipher.Select(c => NumberTheory.ModPow(c, b.D, a.N)).ToList();
        Assert.NotEqual("Таємне повідомлення", RsaCipher.BlocksToText(wrong, a.N));
    }

    [Fact]
    public void EmptyText_GivesNoBlocks()
    {
        var k = RsaKeyPair.Generate(512);
        Assert.Empty(RsaCipher.Encrypt("", k.Public));
        Assert.Equal("", RsaCipher.Decrypt(Array.Empty<BigInteger>(), k.Private));
    }

    [Fact]
    public void FromPrimes_Rejects_InvalidInput()
    {
        Assert.Throws<ArgumentException>(() => RsaKeyPair.FromPrimes(61, 61, 17));
        Assert.Throws<ArgumentException>(() => RsaKeyPair.FromPrimes(60, 53, 17));
        Assert.Throws<ArgumentException>(() => RsaKeyPair.FromPrimes(61, 53, 3)); // НСД(3, 3120) = 3
    }

    [Fact]
    public void EncryptBlock_Rejects_BlockNotLessThanN()
    {
        var k = RsaKeyPair.FromPrimes(61, 53, 17);
        Assert.Throws<ArgumentOutOfRangeException>(() => RsaCipher.EncryptBlock(3233, k.Public));
    }
}
