using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Hash.Core;

/// <summary>Мінімальна робота з PNG: створення, розбір чанків, вставка чанка.</summary>
public static class Png
{
    public static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public record PngChunk(string Type, byte[] Data, int Offset, bool CrcOk);

    /// <summary>Кодує RGB-зображення (8 біт на канал, фільтр 0).</summary>
    public static byte[] Encode(int width, int height, Func<int, int, (byte R, byte G, byte B)> pixel)
    {
        var raw = new byte[height * (1 + width * 3)];
        int k = 0;
        for (int y = 0; y < height; y++)
        {
            raw[k++] = 0; // тип фільтра рядка
            for (int x = 0; x < width; x++)
            {
                var (r, g, b) = pixel(x, y);
                raw[k++] = r; raw[k++] = g; raw[k++] = b;
            }
        }

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; ihdr[9] = 2; // 8 біт, RGB

        using var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true))
            zs.Write(raw);

        using var ms = new MemoryStream();
        ms.Write(Signature);
        ms.Write(Chunk("IHDR", ihdr));
        ms.Write(Chunk("IDAT", z.ToArray()));
        ms.Write(Chunk("IEND", Array.Empty<byte>()));
        return ms.ToArray();
    }

    /// <summary>Чанк: довжина (BE) + тип + дані + CRC32(тип + дані).</summary>
    public static byte[] Chunk(string type, byte[] data)
    {
        var buf = new byte[12 + data.Length];
        BinaryPrimitives.WriteInt32BigEndian(buf, data.Length);
        Encoding.ASCII.GetBytes(type, 0, 4, buf, 4);
        data.CopyTo(buf, 8);
        uint crc = Crc32.Compute(buf.AsSpan(4, 4 + data.Length));
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(8 + data.Length), crc);
        return buf;
    }

    public static bool IsPng(byte[] data) => data.AsSpan().StartsWith(Signature);

    /// <summary>Розбирає файл на чанки та перевіряє CRC кожного.</summary>
    public static List<PngChunk> Parse(byte[] png)
    {
        if (!IsPng(png)) throw new InvalidDataException("Це не PNG");
        var list = new List<PngChunk>();
        int pos = 8;
        while (pos + 12 <= png.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            string type = Encoding.ASCII.GetString(png, pos + 4, 4);
            byte[] data = png.AsSpan(pos + 8, len).ToArray();
            uint crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(pos + 8 + len));
            list.Add(new PngChunk(type, data, pos, crc == Crc32.Compute(png.AsSpan(pos + 4, 4 + len))));
            pos += 12 + len;
            if (type == "IEND") break;
        }
        return list;
    }

    /// <summary>Вставляє чанк безпосередньо перед IEND.</summary>
    public static byte[] InsertBeforeIend(byte[] png, string type, byte[] data)
    {
        var iend = Parse(png).Last(c => c.Type == "IEND");
        var chunk = Chunk(type, data);
        var result = new byte[png.Length + chunk.Length];
        png.AsSpan(0, iend.Offset).CopyTo(result);
        chunk.CopyTo(result, iend.Offset);
        png.AsSpan(iend.Offset).CopyTo(result.AsSpan(iend.Offset + chunk.Length));
        return result;
    }

    /// <summary>Розпаковує IDAT і повертає «сирі» рядки (для перевірки цілісності).</summary>
    public static byte[] DecodeRaw(byte[] png)
    {
        var idat = Parse(png).Where(c => c.Type == "IDAT").SelectMany(c => c.Data).ToArray();
        using var zs = new ZLibStream(new MemoryStream(idat), CompressionMode.Decompress);
        using var ms = new MemoryStream();
        zs.CopyTo(ms);
        return ms.ToArray();
    }
}

/// <summary>CRC-32 (поліном 0xEDB88320), як вимагає специфікація PNG.</summary>
public static class Crc32
{
    static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
