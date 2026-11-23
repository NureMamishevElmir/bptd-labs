using System.Numerics;
using System.Security.Cryptography;

namespace Dh.Core;

/// <summary>Учасник обміну: секретний X і відкритий Y = A^X mod Q.</summary>
public sealed class DhParty
{
    public string Name { get; }
    public DhGroup Group { get; }
    public BigInteger X { get; } // секретний ключ (нікому не передається)
    public BigInteger Y { get; } // відкритий ключ

    public DhParty(string name, DhGroup group, BigInteger? x = null)
    {
        Name = name;
        Group = group;
        if (x is { } given)
        {
            if (given <= 0 || given >= group.Q) throw new ArgumentOutOfRangeException(nameof(x), "Потрібно 0 < X < Q");
            X = given;
            Y = ModMath.ModPow(group.A, X, group.Q);
            return;
        }
        do
        {
            X = ModMath.RandomBelow(group.Q - 3) + 2;   // X з [2, Q-2]
            Y = ModMath.ModPow(group.A, X, group.Q);
        } while (!IsValidY(Y, group));                  // Y = Q-1 непридатний
    }

    /// <summary>Допустимий відкритий ключ: 1 &lt; Y &lt; Q-1.</summary>
    public static bool IsValidY(BigInteger y, DhGroup g) => y > 1 && y < g.Q - 1;

    /// <summary>Спільний ключ K = (Y_іншого)^X_свій mod Q.</summary>
    public BigInteger SharedKey(BigInteger otherY)
    {
        // відкидаємо тривіальні значення: 1 і Q-1 дають передбачуваний K
        if (!IsValidY(otherY, Group))
            throw new ArgumentException("Недопустимий відкритий ключ", nameof(otherY));
        return ModMath.ModPow(otherY, X, Group.Q);
    }

    public BigInteger SharedKey(DhParty other) => SharedKey(other.Y);

    /// <summary>Байти K (big-endian, без знака) — так само, як у браузері.</summary>
    public static byte[] KeyBytes(BigInteger k) => k.ToByteArray(isUnsigned: true, isBigEndian: true);

    /// <summary>Ключ AES-256 = SHA-256(K).</summary>
    public static byte[] AesKey(BigInteger k) => SHA256.HashData(KeyBytes(k));

    /// <summary>Відбиток ключа: перші 8 hex-символів SHA-256(K).</summary>
    public static string Fingerprint(BigInteger k) => Convert.ToHexString(AesKey(k))[..8].ToLowerInvariant();
}
