using System.Text;

namespace Hash.Core;

/// <summary>Спосіб безпечно вставити nonce у файл певного формату.</summary>
public interface INonceInserter
{
    string Method { get; }
    /// <summary>Готує файл один раз і повертає функцію «nonce → нові байти».</summary>
    Func<string, byte[]> Prepare(byte[] data);
}

/// <summary>docx: XML-коментар у word/document.xml, zip перезбирається.</summary>
public sealed class DocxNonce : INonceInserter
{
    public string Method => "docx: <!-- nonce --> у word/document.xml";

    public Func<string, byte[]> Prepare(byte[] data)
    {
        var parts = Docx.Unpack(data);
        string xml = Encoding.UTF8.GetString(parts.First(p => p.Key == Docx.DocumentPart).Value);
        return nonce => Docx.Pack(parts.Select(p => p.Key == Docx.DocumentPart
            ? new KeyValuePair<string, byte[]>(p.Key, Encoding.UTF8.GetBytes(Docx.InsertComment(xml, "nonce: " + nonce)))
            : p));
    }
}

/// <summary>png: додатковий чанк tEXt "nonce" перед IEND.</summary>
public sealed class PngNonce : INonceInserter
{
    public string Method => "png: чанк tEXt перед IEND";

    public Func<string, byte[]> Prepare(byte[] data) =>
        nonce => Png.InsertBeforeIend(data, "tEXt", Encoding.Latin1.GetBytes("nonce\0" + nonce));
}

/// <summary>Вихідний код / текст: коментар у кінці файлу.</summary>
public sealed class CommentNonce : INonceInserter
{
    readonly string _prefix;
    public CommentNonce(string prefix) => _prefix = prefix;
    public string Method => $"текст: рядок «{_prefix} nonce: ...» у кінці";

    public Func<string, byte[]> Prepare(byte[] data)
    {
        bool nl = data.Length > 0 && data[^1] == (byte)'\n';
        return nonce => data.Concat(Encoding.UTF8.GetBytes((nl ? "" : "\n") + $"{_prefix} nonce: {nonce}\n")).ToArray();
    }
}

/// <summary>Будь-який інший файл: дописуємо байти в кінець.</summary>
public sealed class AppendNonce : INonceInserter
{
    public string Method => "інше: байти в кінці файлу";

    public Func<string, byte[]> Prepare(byte[] data) =>
        nonce => data.Concat(Encoding.ASCII.GetBytes("NONCE:" + nonce)).ToArray();
}

public record CollisionResult(
    int Bits, byte Target, byte Before, byte After,
    string Nonce, long Attempts, string Method, byte[] Data);

/// <summary>Пошук колізії: підбираємо nonce, доки дайджест зміненого файлу не збіжиться з оригіналом.</summary>
public static class CollisionFinder
{
    static readonly Dictionary<string, string> CommentPrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "//", [".js"] = "//", [".ts"] = "//", [".java"] = "//", [".c"] = "//", [".cpp"] = "//",
        [".h"] = "//", [".go"] = "//", [".py"] = "#", [".sh"] = "#", [".rb"] = "#", [".ps1"] = "#",
        [".sql"] = "--", [".txt"] = "#",
    };

    /// <summary>Визначає формат за сигнатурою, а для тексту — за розширенням.</summary>
    public static INonceInserter Detect(string fileName, byte[] data)
    {
        if (Png.IsPng(data)) return new PngNonce();
        if (Docx.IsDocx(data)) return new DocxNonce();
        string ext = Path.GetExtension(fileName);
        if (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            ext = Path.GetExtension(Path.GetFileNameWithoutExtension(fileName)) is { Length: > 0 } inner ? inner : ext; // program.cs.txt
        return CommentPrefix.TryGetValue(ext, out var p) ? new CommentNonce(p) : new AppendNonce();
    }

    public static CollisionResult Find(byte[] original, byte[] modified, string fileName, int bits, long maxAttempts = 10_000_000)
    {
        byte target = Hash13.Compute(original, bits);
        byte before = Hash13.Compute(modified, bits);
        var inserter = Detect(fileName, modified);
        if (before == target)
            return new CollisionResult(bits, target, before, before, "", 0, inserter.Method, modified);

        var make = inserter.Prepare(modified);
        for (long i = 1; i <= maxAttempts; i++)
        {
            string nonce = i.ToString("x");
            byte[] candidate = make(nonce);
            if (Hash13.Compute(candidate, bits) == target)
                return new CollisionResult(bits, target, before, target, nonce, i, inserter.Method, candidate);
        }
        throw new InvalidOperationException($"Колізію не знайдено за {maxAttempts} спроб");
    }
}
