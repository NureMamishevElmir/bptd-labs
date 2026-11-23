using System.Numerics;
using System.Security.Cryptography;

namespace Dh.Core;

/// <summary>Модульна арифметика для алгоритму Діффі-Хеллмана.</summary>
public static class ModMath
{
    /// <summary>a^e mod m методом "square-and-multiply" (біти e від молодшого до старшого).</summary>
    public static BigInteger ModPow(BigInteger a, BigInteger e, BigInteger m)
    {
        if (m <= 0) throw new ArgumentOutOfRangeException(nameof(m));
        if (e < 0) throw new ArgumentOutOfRangeException(nameof(e));
        if (m.IsOne) return 0;

        BigInteger result = 1;
        BigInteger b = ((a % m) + m) % m;
        while (e > 0)
        {
            if (!e.IsEven) result = result * b % m; // біт = 1 -> множимо
            b = b * b % m;                          // підносимо до квадрата
            e >>= 1;                                // наступний біт
        }
        return result;
    }

    /// <summary>Список A^1, A^2, ..., A^(Q-1) mod Q (лише для малих Q).</summary>
    public static List<int> Powers(int a, int q)
    {
        var list = new List<int>();
        for (int k = 1; k < q; k++) list.Add((int)ModPow(a, k, q));
        return list;
    }

    /// <summary>Первісний корінь перебором: усі A^k mod Q різні (k = 1..Q-1).</summary>
    public static bool IsPrimitiveRootBrute(int a, int q) =>
        a > 0 && a < q && Powers(a, q).Distinct().Count() == q - 1;

    /// <summary>g — первісний корінь, якщо g^((p-1)/f) != 1 для кожного простого дільника f числа p-1.</summary>
    public static bool IsPrimitiveRoot(BigInteger g, BigInteger p, IEnumerable<BigInteger> primeFactors)
    {
        if (g <= 1 || g >= p) return false;
        return primeFactors.All(f => !ModPow(g, (p - 1) / f, p).IsOne);
    }

    /// <summary>Для безпечного простого p = 2q+1: g^2 != 1 і g^q != 1 (mod p).</summary>
    public static bool IsPrimitiveRootSafePrime(BigInteger g, BigInteger p) =>
        IsPrimitiveRoot(g, p, new[] { (BigInteger)2, (p - 1) / 2 });

    /// <summary>Найменший первісний корінь безпечного простого p.</summary>
    public static BigInteger SmallestPrimitiveRootSafePrime(BigInteger p)
    {
        for (BigInteger g = 2; g < p; g++)
            if (IsPrimitiveRootSafePrime(g, p)) return g;
        throw new InvalidOperationException("Первісний корінь не знайдено");
    }

    /// <summary>Тест простоти Міллера-Рабіна (на власному ModPow).</summary>
    public static bool IsProbablePrime(BigInteger n, int rounds = 32)
    {
        if (n < 2) return false;
        foreach (int sp in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
        {
            if (n == sp) return true;
            if (n % sp == 0) return false;
        }
        BigInteger d = n - 1; int s = 0;
        while (d.IsEven) { d >>= 1; s++; }

        for (int i = 0; i < rounds; i++)
        {
            BigInteger a = RandomBelow(n - 3) + 2; // a з [2, n-2]
            BigInteger x = ModPow(a, d, n);
            if (x.IsOne || x == n - 1) continue;
            bool composite = true;
            for (int r = 1; r < s && composite; r++)
            {
                x = x * x % n;
                if (x == n - 1) composite = false;
            }
            if (composite) return false;
        }
        return true;
    }

    /// <summary>Криптостійке випадкове число з [0, max).</summary>
    public static BigInteger RandomBelow(BigInteger max)
    {
        if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
        int len = max.GetByteCount(isUnsigned: true) + 8; // +8 байт майже усувають зсув розподілу
        var bytes = RandomNumberGenerator.GetBytes(len);
        return new BigInteger(bytes, isUnsigned: true) % max;
    }
}
