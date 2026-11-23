using System.Numerics;
using System.Security.Cryptography;

namespace Rsa.Core;

/// <summary>Генерація випадкових простих чисел і тест Міллера–Рабіна.</summary>
public static class PrimeGenerator
{
    /// <summary>Малі прості до 1000 — для швидкого відсіву пробним діленням.</summary>
    private static readonly int[] SmallPrimes = BuildSmallPrimes(1000);

    private static int[] BuildSmallPrimes(int limit)
    {
        var composite = new bool[limit + 1];
        var list = new List<int>();
        for (int i = 2; i <= limit; i++)
        {
            if (composite[i]) continue;
            list.Add(i);
            for (int j = i * i; j <= limit; j += i) composite[j] = true;
        }
        return list.ToArray();
    }

    /// <summary>Невід'ємне число з криптостійких випадкових байтів.</summary>
    private static BigInteger FromRandomBytes(byte[] bytes) =>
        new(bytes, isUnsigned: true, isBigEndian: true);

    /// <summary>Випадкове непарне число рівно з <paramref name="bits"/> біт (два старші біти = 1).</summary>
    public static BigInteger RandomOddWithBits(int bits)
    {
        if (bits < 3) throw new ArgumentOutOfRangeException(nameof(bits));
        var bytes = RandomNumberGenerator.GetBytes((bits + 7) / 8);
        int extra = bytes.Length * 8 - bits;          // зайві біти у старшому байті
        bytes[0] &= (byte)(0xFF >> extra);
        bytes[0] |= (byte)(0xC0 >> extra);            // два старші біти → N матиме точну довжину
        bytes[^1] |= 1;                               // непарне
        return FromRandomBytes(bytes);
    }

    /// <summary>Рівномірно випадкове число з відрізка [min, max] (метод відкидання).</summary>
    public static BigInteger RandomInRange(BigInteger min, BigInteger max)
    {
        var range = max - min;
        int bits = NumberTheory.BitLength(range);
        if (bits == 0) return min;
        int len = (bits + 7) / 8;
        byte mask = (byte)(0xFF >> (len * 8 - bits));
        while (true)
        {
            var bytes = RandomNumberGenerator.GetBytes(len);
            bytes[0] &= mask;
            var r = FromRandomBytes(bytes);
            if (r <= range) return min + r;
        }
    }

    /// <summary>
    /// Імовірнісний тест Міллера–Рабіна. Ймовірність помилки для складеного числа ≤ 4^(−rounds).
    /// </summary>
    public static bool IsProbablePrime(BigInteger n, int rounds = 40)
    {
        if (n < 2) return false;
        foreach (var p in SmallPrimes)
        {
            if (n == p) return true;
            if (n % p == 0) return false;
        }

        // n − 1 = d · 2^s, d — непарне
        var d = n - 1;
        int s = 0;
        while (d.IsEven) { d /= 2; s++; }

        for (int i = 0; i < rounds; i++)
        {
            var a = RandomInRange(2, n - 2);
            var x = NumberTheory.ModPow(a, d, n);
            if (x == 1 || x == n - 1) continue;

            bool witness = true;                   // a — свідок складеності, доки не знайдемо n − 1
            for (int r = 1; r < s; r++)
            {
                x = x * x % n;
                if (x == n - 1) { witness = false; break; }
            }
            if (witness) return false;
        }
        return true;
    }

    /// <summary>Випадкове просте число заданої довжини в бітах.</summary>
    public static BigInteger GeneratePrime(int bits)
    {
        while (true)
        {
            var candidate = RandomOddWithBits(bits);
            if (IsProbablePrime(candidate)) return candidate;
        }
    }
}
