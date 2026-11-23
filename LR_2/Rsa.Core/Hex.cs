using System.Globalization;
using System.Numerics;

namespace Rsa.Core;

/// <summary>Перетворення невід'ємних великих чисел у шістнадцятковий рядок і назад.</summary>
public static class Hex
{
    public static string ToHex(BigInteger x)
    {
        if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
        var s = x.ToString("X").TrimStart('0');   // ToString("X") може додати знаковий «0»
        return s.Length == 0 ? "0" : s;
    }

    public static BigInteger FromHex(string hex)
    {
        hex = hex.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex[2..];
        if (hex.Length == 0) throw new FormatException("Порожнє шістнадцяткове число.");
        // провідний «0» гарантує додатне значення
        return BigInteger.Parse("0" + hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
