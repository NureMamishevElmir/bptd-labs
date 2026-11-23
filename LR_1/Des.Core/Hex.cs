namespace Des.Core;

/// <summary>Допоміжні перетворення між байтами, ulong і hex-рядками.</summary>
public static class Hex
{
    public static bool IsHex(string s) => s.Length > 0 && s.All(Uri.IsHexDigit);

    /// <summary>Hex-рядок у байти (пробіли ігноруються).</summary>
    public static byte[] ToBytes(string hex)
    {
        hex = new string(hex.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (hex.Length % 2 != 0 || (hex.Length > 0 && !IsHex(hex)))
            throw new ArgumentException("Некоректний hex-рядок: потрібна парна кількість цифр 0-9, A-F.");
        return Convert.FromHexString(hex);
    }

    public static string FromBytes(byte[] data) => Convert.ToHexString(data);

    public static ulong ToUInt64(string hex16) => ToUInt64(ToBytes(hex16), 0);

    /// <summary>8 байтів (big-endian) у 64-бітне число.</summary>
    public static ulong ToUInt64(byte[] data, int offset)
    {
        ulong v = 0;
        for (int i = 0; i < 8; i++) v = (v << 8) | data[offset + i];
        return v;
    }

    /// <summary>64-бітне число у 8 байтів (big-endian).</summary>
    public static void WriteUInt64(ulong v, byte[] dest, int offset)
    {
        for (int i = 7; i >= 0; i--) { dest[offset + i] = (byte)v; v >>= 8; }
    }

    public static string FromUInt64(ulong v) => v.ToString("X16");

    /// <summary>Значення з заданою кількістю бітів у hex (32 біти -> 8 цифр, 48 -> 12).</summary>
    public static string FromBits(ulong v, int bits) => v.ToString("X" + bits / 4);
}
