namespace Des.Core;

public enum CipherMode { ECB, CBC }

/// <summary>
/// Режими шифрування ECB і CBC з доповненням PKCS#7 (блок 8 байтів).
/// </summary>
public static class BlockModes
{
    public const int BlockSize = 8;

    public static byte[] Encrypt(DesCipher des, byte[] data, CipherMode mode, ulong iv = 0)
    {
        byte[] padded = Pad(data);
        var result = new byte[padded.Length];
        ulong prev = iv;

        for (int off = 0; off < padded.Length; off += BlockSize)
        {
            ulong block = Hex.ToUInt64(padded, off);
            if (mode == CipherMode.CBC) block ^= prev;   // C_i = E(P_i xor C_{i-1})
            ulong c = des.EncryptBlock(block);
            Hex.WriteUInt64(c, result, off);
            prev = c;
        }
        return result;
    }

    public static byte[] Decrypt(DesCipher des, byte[] data, CipherMode mode, ulong iv = 0)
    {
        if (data.Length == 0 || data.Length % BlockSize != 0)
            throw new ArgumentException("Довжина шифротексту має бути кратною 8 байтам.");

        var result = new byte[data.Length];
        ulong prev = iv;

        for (int off = 0; off < data.Length; off += BlockSize)
        {
            ulong c = Hex.ToUInt64(data, off);
            ulong p = des.DecryptBlock(c);
            if (mode == CipherMode.CBC) p ^= prev;       // P_i = D(C_i) xor C_{i-1}
            Hex.WriteUInt64(p, result, off);
            prev = c;
        }
        return Unpad(result);
    }

    /// <summary>PKCS#7: додаємо n байтів зі значенням n (1..8), завжди хоча б один.</summary>
    public static byte[] Pad(byte[] data)
    {
        int n = BlockSize - data.Length % BlockSize;
        var padded = new byte[data.Length + n];
        Array.Copy(data, padded, data.Length);
        for (int i = data.Length; i < padded.Length; i++) padded[i] = (byte)n;
        return padded;
    }

    public static byte[] Unpad(byte[] data)
    {
        int n = data[^1];
        bool ok = n >= 1 && n <= BlockSize && data.Skip(data.Length - n).All(b => b == n);
        if (!ok)
            throw new ArgumentException("Некоректне доповнення PKCS#7: невірний ключ, IV або режим.");
        return data[..^n];
    }
}
