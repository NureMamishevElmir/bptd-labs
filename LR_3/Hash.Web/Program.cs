using Hash.Core;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5103");
var app = builder.Build();

// Помилки введення → 400 з текстом для інтерфейсу
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception e) when (e is BadHttpRequestException or InvalidOperationException or InvalidDataException)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsJsonAsync(new { error = e.Message });
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

// Перевірка розрядності дайджесту
static int ParseBits(string? s) =>
    int.TryParse(s, out int b) && Hash13.AllowedBits.Contains(b) ? b : throw new BadHttpRequestException("bits: 2, 4 або 8");

static object Describe(byte v, int bits) => new { value = v, binary = Hash13.ToBinary(v, bits), hex = Hash13.ToHex(v, bits) };

static async Task<byte[]> ReadFile(IFormFile f)
{
    using var ms = new MemoryStream();
    await f.CopyToAsync(ms);
    return ms.ToArray();
}

// Дайджест файлу
app.MapPost("/api/digest", async (HttpRequest req) =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files["file"] ?? throw new BadHttpRequestException("Файл не передано");
    int bits = ParseBits(form["bits"]);
    var data = await ReadFile(file);
    return Results.Ok(new
    {
        name = file.FileName, size = data.Length, bits,
        digest = Describe(Hash13.Compute(data, bits), bits),
        full64 = Hash13.Full(data).ToString("X16"),
    });
});

// Лавинний ефект
app.MapGet("/api/avalanche", (string? bits, int? trials) =>
{
    var list = string.IsNullOrEmpty(bits) ? Hash13.AllowedBits : new[] { ParseBits(bits) };
    int n = Math.Clamp(trials ?? 10_000, 100, 200_000);
    return Results.Ok(list.Select(b => Avalanche.Run(b, n)));
});

// Колізія: оригінал + змінений → змінений з nonce
app.MapPost("/api/collide", async (HttpRequest req) =>
{
    var form = await req.ReadFormAsync();
    var orig = form.Files["original"];
    var mod = form.Files["modified"];
    if (orig is null || mod is null) throw new BadHttpRequestException("Потрібні обидва файли");
    int bits = ParseBits(form["bits"]);

    var r = CollisionFinder.Find(await ReadFile(orig), await ReadFile(mod), mod.FileName, bits);
    string outName = Path.GetFileNameWithoutExtension(mod.FileName) + "_collision" + Path.GetExtension(mod.FileName);
    return Results.Ok(new
    {
        bits, method = r.Method, nonce = r.Nonce, attempts = r.Attempts,
        original = Describe(r.Target, bits),
        before = Describe(r.Before, bits),
        after = Describe(Hash13.Compute(r.Data, bits), bits),
        fileName = outName,
        fileBase64 = Convert.ToBase64String(r.Data),
    });
});

app.Run();
