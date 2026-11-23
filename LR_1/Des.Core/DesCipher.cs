namespace Des.Core;

/// <summary>Один рядок трасування: номер раунду, ключ k_i, L_i, R_i.</summary>
public record RoundTrace(int Round, ulong Key, uint L, uint R);

/// <summary>Повне трасування шифрування одного 64-бітного блоку.</summary>
public record BlockTrace(ulong Input, ulong AfterIP, uint L0, uint R0,
                         List<RoundTrace> Rounds, ulong PreOutput, ulong Output);

/// <summary>
/// Блоковий шифр DES: 16 раундів мережі Фейстеля над 64-бітним блоком.
/// </summary>
public class DesCipher
{
    private readonly ulong[] _keys;

    public ulong Key { get; }
    public IReadOnlyList<ulong> RoundKeys => _keys;

    /// <param name="key">64-бітний ключ (біти парності не впливають на результат).</param>
    /// <param name="allowWeak">true — пропустити перевірку слабких ключів (лише для тестів).</param>
    public DesCipher(ulong key, bool allowWeak = false)
    {
        if (!allowWeak) WeakKeys.EnsureStrong(key);
        Key = key;
        _keys = KeySchedule.Generate(key);
    }

    public ulong EncryptBlock(ulong block) => Process(block, decrypt: false, null);

    public ulong DecryptBlock(ulong block) => Process(block, decrypt: true, null);

    /// <summary>Шифрує блок і записує всі проміжні значення.</summary>
    public BlockTrace TraceEncrypt(ulong block)
    {
        var rounds = new List<RoundTrace>();
        ulong output = Process(block, false, rounds);
        ulong ip = Tables.Permute(block, Tables.IP, 64);
        var last = rounds[^1];
        ulong preOutput = ((ulong)last.R << 32) | last.L;   // R16 L16
        return new BlockTrace(block, ip, (uint)(ip >> 32), (uint)ip, rounds, preOutput, output);
    }

    private ulong Process(ulong block, bool decrypt, List<RoundTrace>? trace)
    {
        ulong ip = Tables.Permute(block, Tables.IP, 64);
        uint l = (uint)(ip >> 32);
        uint r = (uint)ip;

        for (int i = 0; i < 16; i++)
        {
            // При розшифруванні ключі беруться у зворотному порядку
            ulong k = decrypt ? _keys[15 - i] : _keys[i];
            uint newR = l ^ F(r, k);   // R_i = L_{i-1} xor f(R_{i-1}, k_i)
            l = r;                     // L_i = R_{i-1}
            r = newR;
            trace?.Add(new RoundTrace(i + 1, k, l, r));
        }

        // Після 16-го раунду половини міняються місцями: R16 L16
        ulong preOutput = ((ulong)r << 32) | l;
        return Tables.Permute(preOutput, Tables.IPInverse, 64);
    }

    /// <summary>Функція Фейстеля f(R, k): E-розширення, XOR з ключем, S-блоки, перестановка P.</summary>
    public static uint F(uint r, ulong k)
    {
        ulong x = Tables.Permute(r, Tables.E, 32) ^ k;   // 48 бітів
        uint s = 0;
        for (int i = 0; i < 8; i++)
        {
            int six = (int)(x >> (42 - 6 * i)) & 0x3F;          // 6 бітів для S_{i+1}
            int row = ((six & 0x20) >> 4) | (six & 0x01);        // крайні біти — рядок
            int col = (six >> 1) & 0x0F;                         // середні 4 біти — стовпець
            s = (s << 4) | Tables.S[i, row * 16 + col];
        }
        return (uint)Tables.Permute(s, Tables.P, 32);
    }
}
