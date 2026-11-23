using System.Numerics;

namespace Rsa.Core;

/// <summary>
/// Власна реалізація теоретико-числових операцій для RSA.
/// BigInteger використовується лише як контейнер (+, −, ×, ÷, %).
/// </summary>
public static class NumberTheory
{
    /// <summary>Невід'ємний залишок a mod m.</summary>
    public static BigInteger Mod(BigInteger a, BigInteger m)
    {
        var r = a % m;
        return r < 0 ? r + m : r;
    }

    /// <summary>НСД за алгоритмом Евкліда.</summary>
    public static BigInteger Gcd(BigInteger a, BigInteger b)
    {
        a = BigInteger.Abs(a);
        b = BigInteger.Abs(b);
        while (b != 0)
            (a, b) = (b, a % b);
        return a;
    }

    /// <summary>Розширений алгоритм Евкліда: повертає (g, x, y), де a·x + b·y = g = НСД(a, b).</summary>
    public static (BigInteger G, BigInteger X, BigInteger Y) ExtendedGcd(BigInteger a, BigInteger b)
    {
        BigInteger oldR = a, r = b;
        BigInteger oldX = 1, x = 0;
        BigInteger oldY = 0, y = 1;
        while (r != 0)
        {
            var q = oldR / r;
            (oldR, r) = (r, oldR - q * r);
            (oldX, x) = (x, oldX - q * x);
            (oldY, y) = (y, oldY - q * y);
        }
        return (oldR, oldX, oldY);
    }

    /// <summary>Обернений елемент a⁻¹ mod m (існує лише при НСД(a, m) = 1).</summary>
    public static BigInteger ModInverse(BigInteger a, BigInteger m)
    {
        var (g, x, _) = ExtendedGcd(Mod(a, m), m);
        if (g != 1)
            throw new ArgumentException("Обернений елемент не існує: НСД(a, m) ≠ 1.");
        return Mod(x, m);
    }

    /// <summary>
    /// Модульне піднесення до степеня b^e mod m методом «квадрат і множення»
    /// (біти показника переглядаються від молодшого до старшого).
    /// </summary>
    public static BigInteger ModPow(BigInteger b, BigInteger e, BigInteger m)
    {
        if (m <= 0) throw new ArgumentOutOfRangeException(nameof(m), "Модуль має бути додатним.");
        if (e < 0) throw new ArgumentOutOfRangeException(nameof(e), "Показник має бути невід'ємним.");
        if (m == 1) return 0;

        BigInteger result = 1;
        b = Mod(b, m);
        while (e > 0)
        {
            if (!e.IsEven)              // поточний біт = 1 → множимо
                result = result * b % m;
            b = b * b % m;              // підносимо основу до квадрату
            e /= 2;                     // переходимо до наступного біта
        }
        return result;
    }

    /// <summary>Кількість біт у числі (0 для нуля).</summary>
    public static int BitLength(BigInteger x) => x.IsZero ? 0 : (int)x.GetBitLength();
}
