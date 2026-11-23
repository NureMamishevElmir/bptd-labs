using System.Text;
using Des.Core;
using CipherMode = Des.Core.CipherMode;

namespace Des.Tests;

public class DesTests
{
    private static readonly Random Rng = new(13);

    // Класичний контрольний приклад (J. Orlin Grabbe, "The DES Algorithm Illustrated")
    [Fact]
    public void ClassicVector_Encrypts()
    {
        var des = new DesCipher(0x133457799BBCDFF1);
        Assert.Equal(0x85E813540F0AB405UL, des.EncryptBlock(0x0123456789ABCDEF));
        Assert.Equal(0x0123456789ABCDEFUL, des.DecryptBlock(0x85E813540F0AB405));
    }

    [Fact]
    public void ClassicVector_RoundKeysAndTrace()
    {
        var des = new DesCipher(0x133457799BBCDFF1);
        Assert.Equal(0x1B02EFFC7072UL, des.RoundKeys[0]);    // k1
        Assert.Equal(0xCB3D8B0E17F5UL, des.RoundKeys[15]);   // k16

        var t = des.TraceEncrypt(0x0123456789ABCDEF);
        Assert.Equal(0xCC00CCFFF0AAF0AAUL, t.AfterIP);
        Assert.Equal(0xEF4A6544u, t.Rounds[0].R);            // R1
        Assert.Equal(0x0A4CD995u, t.Rounds[15].R);           // R16
        Assert.Equal(0x43423234u, t.Rounds[15].L);           // L16
        Assert.Equal(0x85E813540F0AB405UL, t.Output);
    }

    [Fact]
    public void KnownAnswer_ZeroBlock()
    {
        // Відомий вектор: ключ 0E329232EA6D0D73, блок 8787878787878787 -> 0000000000000000
        var des = new DesCipher(0x0E329232EA6D0D73);
        Assert.Equal(0UL, des.EncryptBlock(0x8787878787878787));
    }

    [Theory]
    [InlineData(CipherMode.ECB)]
    [InlineData(CipherMode.CBC)]
    public void RoundTrip_RandomData(CipherMode mode)
    {
        for (int len = 0; len < 40; len++)
        {
            var data = new byte[len];
            Rng.NextBytes(data);
            var des = new DesCipher(RandomStrongKey());
            ulong iv = (ulong)Rng.NextInt64();
            byte[] enc = BlockModes.Encrypt(des, data, mode, iv);
            Assert.Equal(0, enc.Length % 8);
            Assert.True(enc.Length > data.Length);
            Assert.Equal(data, BlockModes.Decrypt(des, enc, mode, iv));
        }
    }

    [Theory]
    [InlineData(CipherMode.ECB)]
    [InlineData(CipherMode.CBC)]
    public void RoundTrip_UkrainianText(CipherMode mode)
    {
        const string text = "Безпека програм та даних — ЛР1, варіант 13. Їжак, ґанок, ще й єнот!";
        var des = new DesCipher(KeySchedule.ParseKey("Elmir_13"));
        ulong iv = 0x0011223344556677;
        byte[] enc = BlockModes.Encrypt(des, Encoding.UTF8.GetBytes(text), mode, iv);
        string back = Encoding.UTF8.GetString(BlockModes.Decrypt(des, enc, mode, iv));
        Assert.Equal(text, back);
    }

    [Fact]
    public void Cbc_DiffersFromEcb_OnRepeatedBlocks()
    {
        var des = new DesCipher(0x133457799BBCDFF1);
        var data = Encoding.ASCII.GetBytes("AAAAAAAAAAAAAAAA");
        byte[] ecb = BlockModes.Encrypt(des, data, CipherMode.ECB);
        byte[] cbc = BlockModes.Encrypt(des, data, CipherMode.CBC, 0x1234);
        Assert.Equal(ecb[..8], ecb[8..16]);       // ECB: однакові блоки -> однаковий шифротекст
        Assert.NotEqual(cbc[..8], cbc[8..16]);    // CBC: різні
    }

    [Theory]
    [InlineData("0101010101010101")]
    [InlineData("FEFEFEFEFEFEFEFE")]
    [InlineData("E0E0E0E0F1F1F1F1")]
    [InlineData("1F1F1F1F0E0E0E0E")]
    [InlineData("0000000000000000")]   // те саме, що 0101..., без бітів парності
    [InlineData("FFFFFFFFFFFFFFFF")]
    [InlineData("E1E1E1E1F0F0F0F0")]
    public void WeakKeys_Rejected(string hex)
    {
        ulong key = Hex.ToUInt64(hex);
        Assert.True(WeakKeys.IsWeak(key));
        var ex = Assert.Throws<WeakKeyException>(() => new DesCipher(key));
        Assert.Contains("слабким", ex.Message);
    }

    [Fact]
    public void SemiWeakKeys_AllTwelveRejected()
    {
        foreach (var (a, b) in WeakKeys.SemiWeakPairs)
        {
            Assert.Equal(b, WeakKeys.SemiWeakPartner(a));
            Assert.Equal(a, WeakKeys.SemiWeakPartner(b));
            Assert.Throws<WeakKeyException>(() => new DesCipher(a));
            Assert.Throws<WeakKeyException>(() => new DesCipher(b ^ 0x0101010101010101)); // інші біти парності
        }
    }

    [Fact]
    public void SemiWeakPair_EncryptWithOneDecryptsOther()
    {
        var (a, b) = WeakKeys.SemiWeakPairs[0];
        var d1 = new DesCipher(a, allowWeak: true);
        var d2 = new DesCipher(b, allowWeak: true);
        ulong x = 0x0123456789ABCDEF;
        Assert.Equal(x, d2.EncryptBlock(d1.EncryptBlock(x)));
    }

    [Fact]
    public void WeakKey_DoubleEncryptionIsIdentity()
    {
        var des = new DesCipher(0x0101010101010101, allowWeak: true);
        ulong x = 0x0123456789ABCDEF;
        Assert.Equal(x, des.EncryptBlock(des.EncryptBlock(x)));
    }

    [Fact]
    public void Parity_FixedToOdd()
    {
        ulong fixedKey = KeySchedule.FixParity(KeySchedule.ParseKey("Elmir_13"));
        Assert.True(KeySchedule.HasValidParity(fixedKey));
        Assert.Equal(0x0101010101010101UL, KeySchedule.FixParity(0));
        Assert.Equal(0x133457799BBCDFF1UL, KeySchedule.FixParity(0x133457799BBCDFF1)); // вже коректна
        // Біти парності не впливають на шифрування
        var a = new DesCipher(0x133457799BBCDFF1);
        var b = new DesCipher(0x133457799BBCDFF1 ^ 0x0101010101010101);
        Assert.Equal(a.EncryptBlock(42), b.EncryptBlock(42));
    }

    [Fact]
    public void ParseKey_AsciiAndHex()
    {
        Assert.Equal(0x456C6D69725F3133UL, KeySchedule.ParseKey("Elmir_13"));
        Assert.Equal(0x133457799BBCDFF1UL, KeySchedule.ParseKey("133457799bbcdff1"));
        Assert.Throws<ArgumentException>(() => KeySchedule.ParseKey("short"));
        Assert.Throws<ArgumentException>(() => KeySchedule.ParseKey("ключ1234"));
    }

    [Fact]
    public void WrongKey_FailsPaddingOrGivesGarbage()
    {
        var data = Encoding.UTF8.GetBytes("секретне повідомлення");
        byte[] enc = BlockModes.Encrypt(new DesCipher(0x133457799BBCDFF1), data, CipherMode.ECB);
        try
        {
            var dec = BlockModes.Decrypt(new DesCipher(0x0E329232EA6D0D73), enc, CipherMode.ECB);
            Assert.NotEqual(data, dec);
        }
        catch (ArgumentException) { /* очікувано: невірне доповнення */ }
    }

    // Звірка з еталонною реалізацією .NET — ЛИШЕ в тестах
    [Fact]
    public void CrossCheck_WithSystemDes()
    {
        using var sys = System.Security.Cryptography.DES.Create();
        for (int i = 0; i < 50; i++)
        {
            ulong key = RandomStrongKey();
            var keyBytes = new byte[8];
            Hex.WriteUInt64(key, keyBytes, 0);
            sys.Key = keyBytes;

            var block = new byte[16];
            Rng.NextBytes(block);
            byte[] expected = sys.EncryptEcb(block, System.Security.Cryptography.PaddingMode.None);

            var des = new DesCipher(key);
            for (int off = 0; off < 16; off += 8)
            {
                ulong c = des.EncryptBlock(Hex.ToUInt64(block, off));
                Assert.Equal(Hex.ToUInt64(expected, off), c);
            }
        }
    }

    [Fact]
    public void CrossCheck_CbcPkcs7_WithSystemDes()
    {
        using var sys = System.Security.Cryptography.DES.Create();
        ulong key = 0x133457799BBCDFF1, iv = 0x0102030405060708;
        var kb = new byte[8]; Hex.WriteUInt64(key, kb, 0);
        var ivb = new byte[8]; Hex.WriteUInt64(iv, ivb, 0);
        sys.Key = kb;
        var data = Encoding.UTF8.GetBytes("Безпека програм та даних");
        byte[] expected = sys.EncryptCbc(data, ivb, System.Security.Cryptography.PaddingMode.PKCS7);
        Assert.Equal(expected, BlockModes.Encrypt(new DesCipher(key), data, CipherMode.CBC, iv));
    }

    private static ulong RandomStrongKey()
    {
        while (true)
        {
            ulong k = KeySchedule.FixParity((ulong)Rng.NextInt64() ^ ((ulong)Rng.Next() << 63));
            if (!WeakKeys.IsWeak(k) && !WeakKeys.IsSemiWeak(k)) return k;
        }
    }
}
