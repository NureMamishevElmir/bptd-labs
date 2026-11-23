using System.IO.Packaging;
using System.Text;
using System.Xml.Linq;
using Hash.Core;

namespace Hash.Tests;

public class CollisionTests
{
    public static TheoryData<int> Bits => new() { 2, 4, 8 };

    [Theory]
    [MemberData(nameof(Bits))]
    public void DocxCollisionIsValidPackage(int bits)
    {
        var r = CollisionFinder.Find(Samples.NakazOriginal(), Samples.NakazEdited(), "nakaz.docx", bits);
        Assert.Equal(Hash13.Compute(Samples.NakazOriginal(), bits), Hash13.Compute(r.Data, bits));
        Assert.IsType<DocxNonce>(CollisionFinder.Detect("x.docx", r.Data));

        // zip відкривається, усі XML-частини коректні, текст змінено, nonce у коментарі
        foreach (var (name, data) in Docx.Unpack(r.Data))
            if (name.EndsWith(".xml") || name.EndsWith(".rels")) XDocument.Parse(Encoding.UTF8.GetString(data));
        string xml = Docx.ReadDocumentXml(r.Data);
        Assert.Contains("22 грудня", xml);
        if (r.Attempts > 0) Assert.Contains($"<!-- nonce: {r.Nonce} -->", xml); // 0 спроб — змінений файл уже колізійний

        // OPC-пакет відкривається System.IO.Packaging
        using var pkg = Package.Open(new MemoryStream(r.Data), FileMode.Open, FileAccess.Read);
        var part = pkg.GetPart(new Uri("/word/document.xml", UriKind.Relative));
        using var s = part.GetStream();
        Assert.Equal("document", XDocument.Load(s).Root!.Name.LocalName);
    }

    [Theory]
    [MemberData(nameof(Bits))]
    public void PngCollisionHasValidChunks(int bits)
    {
        var r = CollisionFinder.Find(Samples.PictureOriginal(), Samples.PictureEdited(), "picture.png", bits);
        Assert.Equal(Hash13.Compute(Samples.PictureOriginal(), bits), Hash13.Compute(r.Data, bits));

        var chunks = Png.Parse(r.Data);
        Assert.All(chunks, c => Assert.True(c.CrcOk, c.Type));
        Assert.Equal(new[] { "IHDR", "IDAT", "tEXt", "IEND" }, chunks.Select(c => c.Type));
        Assert.Equal("nonce\0" + r.Nonce, Encoding.Latin1.GetString(chunks[2].Data));
        Assert.Equal(160 * (1 + 240 * 3), Png.DecodeRaw(r.Data).Length); // піксельні дані цілі
    }

    [Theory]
    [MemberData(nameof(Bits))]
    public void SourceCodeCollisionAddsComment(int bits)
    {
        var r = CollisionFinder.Find(Samples.CodeOriginal(), Samples.CodeEdited(), "sample.py", bits);
        Assert.Equal(Hash13.Compute(Samples.CodeOriginal(), bits), Hash13.Compute(r.Data, bits));
        string text = Encoding.UTF8.GetString(r.Data);
        Assert.StartsWith(Encoding.UTF8.GetString(Samples.CodeEdited()), text);
        Assert.EndsWith($"# nonce: {r.Nonce}\n", text);
    }

    [Fact]
    public void GenericFileAppendsBytes()
    {
        var a = Encoding.ASCII.GetBytes("binary-ish data A");
        var b = Encoding.ASCII.GetBytes("binary-ish data B");
        var r = CollisionFinder.Find(a, b, "data.bin", 8);
        Assert.IsType<AppendNonce>(CollisionFinder.Detect("data.bin", b));
        Assert.Equal(Hash13.Compute(a, 8), Hash13.Compute(r.Data, 8));
        Assert.InRange(r.Attempts, 1, 5000);
    }

    [Fact]
    public void Crc32KnownValue() =>
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));

    [Fact]
    public void AverageAttemptsNearTwoPowBits()
    {
        // середня кількість спроб для 8 біт ≈ 256
        var rnd = new Random(1);
        double sum = 0;
        const int n = 200;
        for (int i = 0; i < n; i++)
        {
            var a = new byte[32]; rnd.NextBytes(a);
            var b = (byte[])a.Clone(); b[0] ^= 1;
            sum += CollisionFinder.Find(a, b, "x.bin", 8).Attempts;
        }
        Assert.InRange(sum / n, 180, 340);
    }
}
