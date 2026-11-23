namespace Des.Core;

/// <summary>
/// Перевірка слабких і напівслабких ключів DES.
/// Порівняння ведеться без урахування бітів парності (маска 0xFE у кожному байті).
/// </summary>
public static class WeakKeys
{
    private const ulong ParityMask = 0xFEFEFEFEFEFEFEFE;

    // 4 слабкі ключі: усі раундові ключі однакові, E(E(x)) = x
    public static readonly ulong[] Weak =
    {
        0x0101010101010101, 0xFEFEFEFEFEFEFEFE,
        0xE0E0E0E0F1F1F1F1, 0x1F1F1F1F0E0E0E0E
    };

    // 6 пар напівслабких ключів: E_k1(E_k2(x)) = x
    public static readonly (ulong, ulong)[] SemiWeakPairs =
    {
        (0x01FE01FE01FE01FE, 0xFE01FE01FE01FE01),
        (0x1FE01FE00EF10EF1, 0xE01FE01FF10EF10E),
        (0x01E001E001F101F1, 0xE001E001F101F101),
        (0x1FFE1FFE0EFE0EFE, 0xFE1FFE1FFE0EFE0E),
        (0x011F011F010E010E, 0x1F011F010E010E01),
        (0xE0FEE0FEF1FEF1FE, 0xFEE0FEE0FEF1FEF1)
    };

    public static bool IsWeak(ulong key) =>
        Weak.Any(w => (w & ParityMask) == (key & ParityMask));

    /// <summary>Повертає парний напівслабкий ключ або null.</summary>
    public static ulong? SemiWeakPartner(ulong key)
    {
        foreach (var (a, b) in SemiWeakPairs)
        {
            if ((a & ParityMask) == (key & ParityMask)) return b;
            if ((b & ParityMask) == (key & ParityMask)) return a;
        }
        return null;
    }

    public static bool IsSemiWeak(ulong key) => SemiWeakPartner(key) != null;

    /// <summary>Кидає виняток, якщо ключ слабкий або напівслабкий.</summary>
    public static void EnsureStrong(ulong key)
    {
        if (IsWeak(key))
            throw new WeakKeyException(
                $"Ключ {Hex.FromUInt64(key)} є слабким: усі 16 раундових ключів однакові, " +
                "повторне шифрування повертає відкритий текст. Оберіть інший ключ.");

        ulong? partner = SemiWeakPartner(key);
        if (partner != null)
            throw new WeakKeyException(
                $"Ключ {Hex.FromUInt64(key)} є напівслабким (пара — {Hex.FromUInt64(partner.Value)}): " +
                "шифрування одним ключем розшифровує інший. Оберіть інший ключ.");
    }
}

/// <summary>Виняток для слабкого або напівслабкого ключа.</summary>
public class WeakKeyException(string message) : Exception(message);
