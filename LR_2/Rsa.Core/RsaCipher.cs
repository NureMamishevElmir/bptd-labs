using System.Numerics;
using System.Text;

namespace Rsa.Core;

/// <summary>
/// Шифрування/розшифрування тексту («підручниковий» RSA без доповнення).
/// Схема блоків: UTF-8 байти ділимо на блоки по k−1 байт, де k — довжина N у байтах,
/// тому кожен блок як число (big-endian) гарантовано менший за N.
/// </summary>
public static class RsaCipher
{
    /// <summary>Довжина N у байтах (k).</summary>
    public static int ModulusBytes(BigInteger n) => n.GetByteCount(isUnsigned: true);

    /// <summary>Розмір блоку відкритого тексту в байтах (k − 1).</summary>
    public static int BlockBytes(BigInteger n)
    {
        int size = ModulusBytes(n) - 1;
        if (size < 1) throw new ArgumentException("N замалий: потрібно N ≥ 256.");
        return size;
    }

    /// <summary>Байти тексту, розбиті на блоки по k−1 байт.</summary>
    public static List<byte[]> SplitBytes(string text, BigInteger n)
    {
        var data = Encoding.UTF8.GetBytes(text);
        int size = BlockBytes(n);
        return data.Chunk(size).ToList();
    }

    /// <summary>Текст → числові блоки Mᵢ &lt; N.</summary>
    public static List<BigInteger> TextToBlocks(string text, BigInteger n) =>
        SplitBytes(text, n).Select(b => new BigInteger(b, isUnsigned: true, isBigEndian: true)).ToList();

    /// <summary>Числові блоки Mᵢ → текст (усі блоки, крім останнього, доповнюються до k−1 байт).</summary>
    public static string BlocksToText(IReadOnlyList<BigInteger> blocks, BigInteger n)
    {
        int size = BlockBytes(n);
        var bytes = new List<byte>();
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i].ToByteArray(isUnsigned: true, isBigEndian: true);
            if (blocks[i].IsZero) b = Array.Empty<byte>();
            bool last = i == blocks.Count - 1;
            if (!last && b.Length < size)            // відновлюємо провідні нульові байти
                bytes.AddRange(new byte[size - b.Length]);
            bytes.AddRange(b);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    /// <summary>Cᵢ = Mᵢ^E mod N.</summary>
    public static BigInteger EncryptBlock(BigInteger m, RsaPublicKey key)
    {
        if (m < 0 || m >= key.N) throw new ArgumentOutOfRangeException(nameof(m), "Блок має бути 0 ≤ M < N.");
        return NumberTheory.ModPow(m, key.E, key.N);
    }

    /// <summary>Mᵢ = Cᵢ^D mod N.</summary>
    public static BigInteger DecryptBlock(BigInteger c, RsaPrivateKey key)
    {
        if (c < 0 || c >= key.N) throw new ArgumentOutOfRangeException(nameof(c), "Блок має бути 0 ≤ C < N.");
        return NumberTheory.ModPow(c, key.D, key.N);
    }

    /// <summary>Шифрування тексту відкритим ключем.</summary>
    public static List<BigInteger> Encrypt(string text, RsaPublicKey key) =>
        TextToBlocks(text, key.N).Select(m => EncryptBlock(m, key)).ToList();

    /// <summary>Розшифрування блоків закритим ключем.</summary>
    public static string Decrypt(IEnumerable<BigInteger> cipher, RsaPrivateKey key) =>
        BlocksToText(cipher.Select(c => DecryptBlock(c, key)).ToList(), key.N);
}
