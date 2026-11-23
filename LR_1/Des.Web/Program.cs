using System.Text;
using Des.Core;
using CipherMode = Des.Core.CipherMode;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5101");
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Інформація про ключ: парність, перевірка на слабкість, 16 раундових ключів
app.MapPost("/api/key", (KeyRequest req) => Safe(() =>
{
    ulong key = KeySchedule.ParseKey(req.Key);
    ulong fixedKey = KeySchedule.FixParity(key);
    string status = WeakKeys.IsWeak(key) ? "weak" : WeakKeys.IsSemiWeak(key) ? "semiweak" : "ok";
    string? message = null;
    try { WeakKeys.EnsureStrong(key); } catch (WeakKeyException e) { message = e.Message; }

    var bytes = Enumerable.Range(0, 8).Select(i =>
    {
        byte b = (byte)(key >> (56 - 8 * i)), f = (byte)(fixedKey >> (56 - 8 * i));
        return new { original = Bin(b), corrected = Bin(f), changed = b != f };
    });

    return Results.Ok(new
    {
        source = req.Key.Trim().Length == 16 ? "hex" : "ascii",
        keyHex = Hex.FromUInt64(key),
        parityFixedHex = Hex.FromUInt64(fixedKey),
        parityOk = key == fixedKey,
        bytes,
        status,
        message,
        roundKeys = KeySchedule.Generate(key).Select(k => Hex.FromBits(k, 48))
    });
}));

// Шифрування тексту (UTF-8) або hex
app.MapPost("/api/encrypt", (CryptRequest req) => Safe(() =>
{
    var des = new DesCipher(KeySchedule.ParseKey(req.Key));
    byte[] data = req.Format == "hex" ? Hex.ToBytes(req.Data) : Encoding.UTF8.GetBytes(req.Data);
    var (mode, iv) = ParseMode(req);
    byte[] enc = BlockModes.Encrypt(des, data, mode, iv);
    return Results.Ok(new
    {
        hex = Hex.FromBytes(enc),
        base64 = Convert.ToBase64String(enc),
        plainHex = Hex.FromBytes(data),
        blocks = enc.Length / 8
    });
}));

// Розшифрування з hex або Base64
app.MapPost("/api/decrypt", (CryptRequest req) => Safe(() =>
{
    var des = new DesCipher(KeySchedule.ParseKey(req.Key));
    byte[] data = req.Format == "base64" ? Convert.FromBase64String(req.Data.Trim()) : Hex.ToBytes(req.Data);
    var (mode, iv) = ParseMode(req);
    byte[] dec = BlockModes.Decrypt(des, data, mode, iv);

    string? text;
    try { text = new UTF8Encoding(false, true).GetString(dec); }
    catch (DecoderFallbackException) { text = null; }   // не UTF-8 — показуємо лише hex

    return Results.Ok(new { text, hex = Hex.FromBytes(dec) });
}));

// Трасування одного 64-бітного блоку
app.MapPost("/api/trace", (TraceRequest req) => Safe(() =>
{
    string block = req.Block.Replace(" ", "");
    if (block.Length != 16 || !Hex.IsHex(block))
        throw new ArgumentException("Блок має містити рівно 16 шістнадцяткових цифр (64 біти).");

    var des = new DesCipher(KeySchedule.ParseKey(req.Key));
    var t = des.TraceEncrypt(Hex.ToUInt64(block));
    return Results.Ok(new
    {
        input = Hex.FromUInt64(t.Input),
        afterIP = Hex.FromUInt64(t.AfterIP),
        l0 = Hex.FromBits(t.L0, 32),
        r0 = Hex.FromBits(t.R0, 32),
        rounds = t.Rounds.Select(r => new
        {
            round = r.Round,
            key = Hex.FromBits(r.Key, 48),
            l = Hex.FromBits(r.L, 32),
            r = Hex.FromBits(r.R, 32)
        }),
        preOutput = Hex.FromUInt64(t.PreOutput),
        output = Hex.FromUInt64(t.Output),
        decrypted = Hex.FromUInt64(des.DecryptBlock(t.Output))
    });
}));

app.Run();

// Перетворює помилки введення і слабкий ключ у відповідь 400 з текстом
static IResult Safe(Func<IResult> action)
{
    try { return action(); }
    catch (WeakKeyException e) { return Results.BadRequest(new { error = e.Message, weakKey = true }); }
    catch (Exception e) when (e is ArgumentException or FormatException)
    {
        return Results.BadRequest(new { error = e.Message });
    }
}

static (CipherMode, ulong) ParseMode(CryptRequest req)
{
    if (req.Mode?.ToUpperInvariant() != "CBC") return (CipherMode.ECB, 0);
    string iv = (req.Iv ?? "").Replace(" ", "");
    if (iv.Length != 16 || !Hex.IsHex(iv))
        throw new ArgumentException("Для режиму CBC потрібен вектор ініціалізації IV — 16 hex-цифр.");
    return (CipherMode.CBC, Hex.ToUInt64(iv));
}

static string Bin(byte b) => Convert.ToString(b, 2).PadLeft(8, '0');

record KeyRequest(string Key);
record CryptRequest(string Data, string Format, string Key, string Mode, string? Iv);
record TraceRequest(string Block, string Key);
