using System.Numerics;

namespace Hash.Core;

/// <summary>Результат експерименту лавинного ефекту.</summary>
public record AvalancheResult(
    int Bits,
    int Trials,
    double AvgChangedPercent,     // середня частка змінених бітів дайджесту, %
    double DigestChangedPercent,  // частка змін, після яких дайджест став іншим, %
    double ExpectedChangedPercent,// теоретично для ідеальної функції: 1 - 2^-bits
    double Full64ChangedPercent); // середня частка змінених бітів 64-бітного стану, %

/// <summary>Лавинний ефект: змінюємо один випадковий байт і рахуємо змінені біти.</summary>
public static class Avalanche
{
    public static AvalancheResult Run(int bits, int trials = 10_000, int seed = 13)
    {
        var rnd = new Random(seed);
        long changedBits = 0, changed64 = 0;
        int changedDigests = 0;

        for (int i = 0; i < trials; i++)
        {
            var msg = new byte[rnd.Next(1, 257)];      // випадкова довжина 1..256
            rnd.NextBytes(msg);
            ulong a = Hash13.Full(msg);

            int pos = rnd.Next(msg.Length);
            msg[pos] ^= (byte)rnd.Next(1, 256);        // байт гарантовано змінюється
            ulong b = Hash13.Full(msg);

            int diff = BitOperations.PopCount((uint)(Hash13.Fold(a, bits) ^ Hash13.Fold(b, bits)));
            changedBits += diff;
            if (diff > 0) changedDigests++;
            changed64 += BitOperations.PopCount(a ^ b);
        }

        return new AvalancheResult(bits, trials,
            100.0 * changedBits / ((double)trials * bits),
            100.0 * changedDigests / trials,
            100.0 * (1 - Math.Pow(2, -bits)),
            100.0 * changed64 / (trials * 64.0));
    }
}
