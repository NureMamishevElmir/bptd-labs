using System.Numerics;
using System.Text;

namespace Des.Core;

/// <summary>
/// Робота з ключем: розбір, біти парності, генерація 16 раундових ключів.
/// </summary>
public static class KeySchedule
{
    /// <summary>
    /// Розбирає ключ: 16 hex-цифр або рівно 8 символів ASCII.
    /// </summary>
    public static ulong ParseKey(string key)
    {
        key = key?.Trim() ?? "";
        if (key.Length == 16 && Hex.IsHex(key))
            return Hex.ToUInt64(key);

        byte[] bytes = Encoding.ASCII.GetBytes(key);
        bool isAscii = key.All(c => c < 128);
        if (bytes.Length == 8 && isAscii)
            return Hex.ToUInt64(bytes, 0);

        throw new ArgumentException(
            "Ключ має бути 8 символами ASCII або 16 шістнадцятковими цифрами.");
    }

    /// <summary>
    /// Виправляє біти парності: молодший біт кожного байта підбирається так,
    /// щоб у байті була непарна кількість одиниць.
    /// </summary>
    public static ulong FixParity(ulong key)
    {
        ulong result = 0;
        for (int i = 7; i >= 0; i--)
        {
            byte b = (byte)(key >> (i * 8));
            int ones = BitOperations.PopCount((uint)(b & 0xFE));
            b = (byte)((b & 0xFE) | (ones % 2 == 0 ? 1 : 0));
            result = (result << 8) | b;
        }
        return result;
    }

    /// <summary>Чи має кожен байт ключа непарну кількість одиниць.</summary>
    public static bool HasValidParity(ulong key) => FixParity(key) == key;

    /// <summary>
    /// Генерує 16 раундових 48-бітних ключів k1..k16.
    /// Біти парності (8, 16, ..., 64) відкидаються таблицею PC-1.
    /// </summary>
    public static ulong[] Generate(ulong key)
    {
        ulong cd = Tables.Permute(key, Tables.PC1, 64);  // 56 бітів
        uint c = (uint)(cd >> 28) & 0x0FFFFFFF;           // C0
        uint d = (uint)cd & 0x0FFFFFFF;                   // D0

        var keys = new ulong[16];
        for (int i = 0; i < 16; i++)
        {
            c = RotateLeft28(c, Tables.Shifts[i]);
            d = RotateLeft28(d, Tables.Shifts[i]);
            ulong joined = ((ulong)c << 28) | d;
            keys[i] = Tables.Permute(joined, Tables.PC2, 56);
        }
        return keys;
    }

    // Циклічний зсув 28-бітного числа вліво
    private static uint RotateLeft28(uint x, int n) =>
        ((x << n) | (x >> (28 - n))) & 0x0FFFFFFF;
}
