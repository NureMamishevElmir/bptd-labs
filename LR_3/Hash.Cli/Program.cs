using System.Text;
using Hash.Core;

Console.OutputEncoding = Encoding.UTF8;

try
{
    return args.FirstOrDefault() switch
    {
        "digest" when args.Length >= 2 => Digest(args[1], Bits(args, 2)),
        "collide" when args.Length >= 4 => Collide(args[1], args[2], args[3], Bits(args, 4)),
        "avalanche" => AvalancheCmd(args.Length >= 2 ? new[] { int.Parse(args[1]) } : Hash13.AllowedBits),
        "make-samples" when args.Length >= 2 => MakeSamples(args[1]),
        _ => Usage(),
    };
}
catch (Exception e)
{
    Console.Error.WriteLine("Помилка: " + e.Message);
    return 1;
}

static int Bits(string[] a, int i) => a.Length > i ? int.Parse(a[i]) : 8;

static string Fmt(byte v, int bits) => $"{Hash13.ToBinary(v, bits)} ({Hash13.ToHex(v, bits)})";

static int Usage()
{
    Console.WriteLine("""
        Hash.Cli — хеш-функція ЛР-3 (варіант 13)
          digest <файл> [біти]                     дайджест файлу (2/4/8, за замовчуванням 8)
          collide <оригінал> <змінений> <вихід> [біти]  підбір nonce для колізії
          avalanche [біти]                          лавинний ефект (10 000 змін)
          make-samples <тека>                       згенерувати три документи та їх колізії
        """);
    return 2;
}

static int Digest(string path, int bits)
{
    var data = File.ReadAllBytes(path);
    Console.WriteLine($"{Path.GetFileName(path)}: {Fmt(Hash13.Compute(data, bits), bits)}  [64 біти: {Hash13.Full(data):X16}]");
    return 0;
}

static int Collide(string orig, string mod, string output, int bits)
{
    var r = CollisionFinder.Find(File.ReadAllBytes(orig), File.ReadAllBytes(mod), mod, bits);
    File.WriteAllBytes(output, r.Data);
    Console.WriteLine($"Спосіб: {r.Method}");
    Console.WriteLine($"Оригінал: {Fmt(r.Target, bits)}; змінений до nonce: {Fmt(r.Before, bits)}");
    Console.WriteLine($"nonce = {r.Nonce}, спроб: {r.Attempts}; результат: {Fmt(Hash13.Compute(r.Data, bits), bits)} → {output}");
    return 0;
}

static int AvalancheCmd(IEnumerable<int> bitsList)
{
    Console.WriteLine("біти | змінених бітів, % | змінився дайджест, % | ідеал, % | 64-біт стан, %");
    foreach (int bits in bitsList)
    {
        var r = Avalanche.Run(bits);
        Console.WriteLine($"{bits,4} | {r.AvgChangedPercent,17:F2} | {r.DigestChangedPercent,20:F2} | {r.ExpectedChangedPercent,8:F2} | {r.Full64ChangedPercent,14:F2}");
    }
    return 0;
}

static int MakeSamples(string dir)
{
    Directory.CreateDirectory(dir);
    var sets = new (string Name, string Ext, byte[] Orig, byte[] Edit, string What)[]
    {
        ("nakaz", ".docx", Samples.NakazOriginal(), Samples.NakazEdited(), "дата «15 грудня» → «22 грудня»"),
        ("sample", ".py", Samples.CodeOriginal(), Samples.CodeEdited(), "RATE = 0.20 → RATE = 0.25"),
        ("picture", ".png", Samples.PictureOriginal(), Samples.PictureEdited(), "колір сонця жовтий → помаранчевий"),
    };

    var report = new StringBuilder("Файл | біти | оригінал | що змінено | змінений до nonce | nonce | спроб | після nonce\n");
    foreach (var s in sets)
    {
        File.WriteAllBytes(Path.Combine(dir, s.Name + s.Ext), s.Orig);
        File.WriteAllBytes(Path.Combine(dir, s.Name + "_edit" + s.Ext), s.Edit); // зміна без nonce
        foreach (int bits in new[] { 8, 4, 2 })
        {
            var r = CollisionFinder.Find(s.Orig, s.Edit, s.Name + s.Ext, bits);
            string suffix = bits == 8 ? "_changed" : $"_changed_{bits}bit";
            File.WriteAllBytes(Path.Combine(dir, s.Name + suffix + s.Ext), r.Data);
            report.AppendLine($"{s.Name}{s.Ext} | {bits} | {Fmt(r.Target, bits)} | {s.What} | {Fmt(r.Before, bits)} | {r.Nonce} | {r.Attempts} | {Fmt(Hash13.Compute(r.Data, bits), bits)}");
        }
    }
    File.WriteAllText(Path.Combine(dir, "results.txt"), report.ToString());
    Console.Write(report);
    return 0;
}
