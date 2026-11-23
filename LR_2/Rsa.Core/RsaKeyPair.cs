using System.Numerics;

namespace Rsa.Core;

/// <summary>Відкритий ключ (E, N).</summary>
public sealed record RsaPublicKey(BigInteger E, BigInteger N);

/// <summary>Закритий ключ (D, N).</summary>
public sealed record RsaPrivateKey(BigInteger D, BigInteger N);

/// <summary>Повна ключова пара RSA разом з проміжними величинами P, Q, φ(N).</summary>
public sealed class RsaKeyPair
{
    /// <summary>Прості числа Ферма, які дозволено обирати як E.</summary>
    public static readonly int[] FermatExponents = { 17, 257, 65537 };

    public BigInteger P { get; }
    public BigInteger Q { get; }
    public BigInteger N { get; }
    public BigInteger Phi { get; }
    public BigInteger E { get; }
    public BigInteger D { get; }

    public RsaPublicKey Public => new(E, N);
    public RsaPrivateKey Private => new(D, N);

    /// <summary>Розмір N у бітах.</summary>
    public int Bits => NumberTheory.BitLength(N);

    private RsaKeyPair(BigInteger p, BigInteger q, BigInteger e)
    {
        P = p;
        Q = q;
        N = p * q;                                  // модуль
        Phi = (p - 1) * (q - 1);                    // функція Ейлера
        E = e;
        D = NumberTheory.ModInverse(e, Phi);        // D·E ≡ 1 (mod φ)
    }

    /// <summary>Побудова ключів із заданих простих P, Q та показника E.</summary>
    public static RsaKeyPair FromPrimes(BigInteger p, BigInteger q, BigInteger e)
    {
        if (p == q) throw new ArgumentException("P і Q мають бути різними.");
        if (!PrimeGenerator.IsProbablePrime(p) || !PrimeGenerator.IsProbablePrime(q))
            throw new ArgumentException("P і Q мають бути простими.");
        var phi = (p - 1) * (q - 1);
        if (e <= 1 || e >= phi || NumberTheory.Gcd(e, phi) != 1)
            throw new ArgumentException("E має бути взаємно простим з φ(N) і 1 < E < φ(N).");
        return new RsaKeyPair(p, q, e);
    }

    /// <summary>Генерація випадкової пари ключів із модулем N рівно <paramref name="bits"/> біт.</summary>
    public static RsaKeyPair Generate(int bits, int e = 65537)
    {
        if (bits < 16) throw new ArgumentOutOfRangeException(nameof(bits));
        int pBits = bits / 2, qBits = bits - pBits;
        while (true)
        {
            var p = PrimeGenerator.GeneratePrime(pBits);
            var q = PrimeGenerator.GeneratePrime(qBits);
            // E має бути взаємно простим з (P−1) та (Q−1), інакше D не існує
            if (p == q || NumberTheory.Gcd(e, p - 1) != 1 || NumberTheory.Gcd(e, q - 1) != 1)
                continue;
            return new RsaKeyPair(p, q, e);
        }
    }
}
