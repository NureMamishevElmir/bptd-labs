using System.Numerics;

namespace Hash.Core;

/// <summary>
/// Власна хеш-функція (варіант 13). Внутрішній стан — 64 біти,
/// результат згортається XOR-ом до 2, 4 або 8 біт.
/// </summary>
public static class Hash13
{
    public static readonly int[] AllowedBits = { 2, 4, 8 };

    const ulong Seed = 0x1313_C0DE_2026_0013UL; // початковий стан (IV)
    const ulong Mul = 0x9E37_79B9_7F4A_7C15UL;  // непарний множник (золотий перетин * 2^64)
    const int Rot = 27;                          // циклічний зсув після множення

    /// <summary>Повний 64-бітний стан після фіналізації.</summary>
    public static ulong Full(ReadOnlySpan<byte> data)
    {
        ulong h = Seed;
        foreach (byte b in data)
        {
            h ^= b;                              // підмішуємо байт
            h *= Mul;                            // поширюємо біти вгору
            h = BitOperations.RotateLeft(h, Rot); // повертаємо старші біти вниз
        }
        h ^= (ulong)data.Length;                 // довжина повідомлення (як у Меркла–Дамгарда)
        h *= Mul;
        return Finalize(h);
    }

    /// <summary>Фінальний перемішувач (xorshift-multiply), бієктивний.</summary>
    static ulong Finalize(ulong h)
    {
        h ^= h >> 33;
        h *= 0xFF51_AFD7_ED55_8CCDUL;
        h ^= h >> 29;
        h *= 0xC4CE_B9FE_1A85_EC53UL;
        h ^= h >> 32;
        return h;
    }

    /// <summary>Дайджест на 2, 4 або 8 біт.</summary>
    public static byte Compute(ReadOnlySpan<byte> data, int bits) => Fold(Full(data), bits);

    /// <summary>XOR-згортка: 64 → 32 → 16 → 8 → 4 → 2.</summary>
    public static byte Fold(ulong h, int bits)
    {
        if (Array.IndexOf(AllowedBits, bits) < 0)
            throw new ArgumentOutOfRangeException(nameof(bits), "Допустимо 2, 4 або 8 біт");
        int width = 64;
        while (width > bits)
        {
            width /= 2;
            h = (h ^ (h >> width)) & ((1UL << width) - 1);
        }
        return (byte)h;
    }

    public static string ToBinary(byte value, int bits) => Convert.ToString(value, 2).PadLeft(bits, '0');

    public static string ToHex(byte value, int bits) => "0x" + value.ToString(bits == 8 ? "X2" : "X1");
}
