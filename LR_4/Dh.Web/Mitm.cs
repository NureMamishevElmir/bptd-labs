using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Dh.Core;

namespace Dh.Web;

/// <summary>AES-GCM як у браузері (WebCrypto): ключ = SHA-256(K), шифротекст || тег(16 байт).
/// Використовується лише зловмисним сервером у демонстрації MITM.</summary>
public static class Mitm
{
    public static string Decrypt(BigInteger k, string ivB64, string ctB64)
    {
        byte[] iv = Convert.FromBase64String(ivB64), data = Convert.FromBase64String(ctB64);
        byte[] ct = data[..^16], tag = data[^16..], plain = new byte[ct.Length];
        using var aes = new AesGcm(DhParty.AesKey(k), 16);
        aes.Decrypt(iv, ct, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    public static (string Iv, string Ct) Encrypt(BigInteger k, string text)
    {
        byte[] iv = RandomNumberGenerator.GetBytes(12), plain = Encoding.UTF8.GetBytes(text);
        byte[] ct = new byte[plain.Length], tag = new byte[16];
        using var aes = new AesGcm(DhParty.AesKey(k), 16);
        aes.Encrypt(iv, plain, ct, tag);
        return (Convert.ToBase64String(iv), Convert.ToBase64String([.. ct, .. tag]));
    }
}
