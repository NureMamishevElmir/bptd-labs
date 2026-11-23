using System.Text;
using Hash.Core;

namespace Hash.Tests;

public class HashTests
{
    [Fact]
    public void Deterministic()
    {
        var data = Encoding.UTF8.GetBytes("Безпека програм та даних");
        Assert.Equal(Hash13.Full(data), Hash13.Full(data.ToArray()));
        foreach (int bits in Hash13.AllowedBits)
            Assert.Equal(Hash13.Compute(data, bits), Hash13.Compute(data, bits));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void OutputRangeAndAllValuesReachable(int bits)
    {
        var seen = new HashSet<byte>();
        for (int i = 0; i < 5000; i++)
        {
            byte d = Hash13.Compute(BitConverter.GetBytes(i), bits);
            Assert.InRange(d, 0, (1 << bits) - 1);
            seen.Add(d);
        }
        Assert.Equal(1 << bits, seen.Count); // використовуються всі значення
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(16)]
    public void InvalidBitsRejected(int bits) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Hash13.Compute(new byte[] { 1 }, bits));

    [Fact]
    public void EmptyAndZeroBytesDiffer()
    {
        // довжина входить у хеш: "", "\0", "\0\0" дають різні 64-бітні стани
        Assert.NotEqual(Hash13.Full(Array.Empty<byte>()), Hash13.Full(new byte[1]));
        Assert.NotEqual(Hash13.Full(new byte[1]), Hash13.Full(new byte[2]));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void AvalancheAtLeast30Percent(int bits)
    {
        var r = Avalanche.Run(bits, 10_000);
        Assert.True(r.AvgChangedPercent >= 30, $"{bits} біт: {r.AvgChangedPercent:F2}%");
        Assert.InRange(r.AvgChangedPercent, 45, 55);              // ≈ 50%
        Assert.InRange(r.Full64ChangedPercent, 48, 52);           // 64-бітний стан теж ≈ 50%
        Assert.InRange(r.DigestChangedPercent, r.ExpectedChangedPercent - 2, r.ExpectedChangedPercent + 2);
    }
}
