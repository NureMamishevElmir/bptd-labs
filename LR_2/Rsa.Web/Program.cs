using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Rsa.Core;
using Rsa.Web;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5102");
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Ключова пара сервера генерується один раз під час запуску
const int ServerKeyBits = 1024;
var serverKey = RsaKeyPair.Generate(ServerKeyBits);
app.Logger.LogInformation("Ключ сервера: {Bits} біт, E = {E}", serverKey.Bits, serverKey.E);

// 1. Генерація ключів заданого розміру
app.MapPost("/api/keys", (KeyRequest req) => Safe(() =>
{
    if (req.Bits is not (512 or 1024 or 2048)) throw new ArgumentException("Розмір N: 512, 1024 або 2048 біт.");
    if (!RsaKeyPair.FermatExponents.Contains(req.E)) throw new ArgumentException("E: 17, 257 або 65537.");

    var sw = Stopwatch.StartNew();
    var k = RsaKeyPair.Generate(req.Bits, req.E);
    sw.Stop();
    var check = k.D * k.E % k.Phi;                  // має дорівнювати 1
    return Results.Ok(new KeyResponse(k.Bits, View(k.P), View(k.Q), View(k.N), View(k.Phi),
        View(k.E), View(k.D), $"D·E mod φ(N) = {check}", check == 1,
        RsaCipher.BlockBytes(k.N), sw.ElapsedMilliseconds));
}));

// 1a. Покрокова демонстрація на малих числах
app.MapPost("/api/demo-small", (SmallDemoRequest req) => Safe(() =>
{
    BigInteger p = Dec(req.P), q = Dec(req.Q), e = Dec(req.E), m = Dec(req.M);
    var k = RsaKeyPair.FromPrimes(p, q, e);
    if (m < 0 || m >= k.N) throw new ArgumentException($"M має бути в межах 0 ≤ M < N = {k.N}.");
    var (_, x, y) = NumberTheory.ExtendedGcd(e, k.Phi);
    var c = RsaCipher.EncryptBlock(m, k.Public);
    var m2 = RsaCipher.DecryptBlock(c, k.Private);

    var steps = new List<string>
    {
        $"1. Прості числа (тест Міллера–Рабіна пройдено): P = {p}, Q = {q}",
        $"2. N = P·Q = {p}·{q} = {k.N}",
        $"3. φ(N) = (P−1)(Q−1) = {p - 1}·{q - 1} = {k.Phi}",
        $"4. E = {e}, НСД(E, φ(N)) = {NumberTheory.Gcd(e, k.Phi)}",
        $"5. Розширений Евклід: {e}·({x}) + {k.Phi}·({y}) = 1  ⇒  D = {x} mod {k.Phi} = {k.D}",
        $"6. Перевірка: D·E mod φ(N) = {k.D}·{e} mod {k.Phi} = {k.D * e % k.Phi}",
        $"7. Відкритий ключ (E, N) = ({e}, {k.N}); закритий ключ (D, N) = ({k.D}, {k.N})",
        $"8. Шифрування: C = M^E mod N = {m}^{e} mod {k.N} = {c}",
    };
    steps.AddRange(PowTrace(m, e, k.N).Select(s => "     " + s));
    steps.Add($"9. Розшифрування: M = C^D mod N = {c}^{k.D} mod {k.N} = {m2} {(m2 == m ? "✓ збігається з M" : "✗")}");
    return Results.Ok(new SmallDemoResponse(steps.ToArray(), k.N.ToString(), k.Phi.ToString(),
        k.D.ToString(), c.ToString(), m2.ToString()));
}));

// 2. Шифрування тексту відкритим ключем (E, N)
app.MapPost("/api/encrypt", (EncryptRequest req) => Safe(() =>
{
    var key = new RsaPublicKey(Hex.FromHex(req.E), Hex.FromHex(req.N));
    var chunks = RsaCipher.SplitBytes(req.Text ?? "", key.N);
    var blocks = chunks.Select((bytes, i) =>
    {
        var m = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var c = RsaCipher.EncryptBlock(m, key);
        return new BlockView(i + 1, Convert.ToHexString(bytes), m.ToString(), Hex.ToHex(m), Hex.ToHex(c));
    }).ToArray();
    return Results.Ok(new EncryptResponse(RsaCipher.BlockBytes(key.N), blocks));
}));

// 2a. Розшифрування блоків закритим ключем (D, N)
app.MapPost("/api/decrypt", (DecryptRequest req) => Safe(() =>
{
    var key = new RsaPrivateKey(Hex.FromHex(req.D), Hex.FromHex(req.N));
    var cs = req.CipherBlocks.Where(s => !string.IsNullOrWhiteSpace(s)).Select(Hex.FromHex).ToList();
    var ms = cs.Select(c => RsaCipher.DecryptBlock(c, key)).ToList();
    var blocks = ms.Select((m, i) => new BlockView(i + 1,
        Convert.ToHexString(m.IsZero ? Array.Empty<byte>() : m.ToByteArray(true, true)),
        m.ToString(), Hex.ToHex(m), Hex.ToHex(cs[i]))).ToArray();
    return Results.Ok(new DecryptResponse(blocks, RsaCipher.BlocksToText(ms, key.N)));
}));

// 3. Відкритий ключ сервера для веб-клієнта
app.MapGet("/api/server-key", () => Results.Ok(new ServerKeyResponse(
    serverKey.Bits, Hex.ToHex(serverKey.E), Hex.ToHex(serverKey.N), RsaCipher.BlockBytes(serverKey.N))));

// 3a. Обмін: сервер розшифровує повідомлення клієнта і відповідає, шифруючи ключем клієнта
app.MapPost("/api/exchange", (ExchangeRequest req) => Safe(() =>
{
    var clientKey = new RsaPublicKey(Hex.FromHex(req.ClientPublicKey.E), Hex.FromHex(req.ClientPublicKey.N));
    if (clientKey.N < 256 * 256) throw new ArgumentException("Ключ клієнта замалий.");

    var ms = req.CiphertextBlocks.Select(h => RsaCipher.DecryptBlock(Hex.FromHex(h), serverKey.Private)).ToList();
    var text = RsaCipher.BlocksToText(ms, serverKey.N);
    int length = new StringInfo(text).LengthInTextElements;
    var reply = $"Сервер отримав: \"{text}\" о {DateTime.Now:HH:mm:ss}, довжина {length} символів";

    var replyBlocks = RsaCipher.Encrypt(reply, clientKey).Select(Hex.ToHex).ToArray();
    app.Logger.LogInformation("Обмін: отримано \"{Text}\", відповідь {Count} блоків", text, replyBlocks.Length);
    return Results.Ok(new ExchangeResponse(replyBlocks,
        new ServerView(ms.Select(Hex.ToHex).ToArray(), text, reply)));
}));

app.Run();

// Допоміжні функції
static NumberView View(BigInteger x) => new(Hex.ToHex(x), x.ToString());

static BigInteger Dec(string s) => BigInteger.Parse(s.Trim(), CultureInfo.InvariantCulture);

static IResult Safe(Func<IResult> action)
{
    try { return action(); }
    catch (Exception ex) when (ex is ArgumentException or FormatException or NullReferenceException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}

// Протокол обчислення b^e mod m методом «квадрат і множення» (для звіту)
static IEnumerable<string> PowTrace(BigInteger b, BigInteger e, BigInteger m)
{
    BigInteger result = 1, x = b % m;
    var bits = "";
    for (var t = e; t > 0; t /= 2) bits = (t.IsEven ? "0" : "1") + bits;
    yield return $"E = {e} = {bits}₂; біти від молодшого:";
    int i = 0;
    for (var t = e; t > 0; t /= 2, i++)
    {
        if (!t.IsEven) result = result * x % m;
        yield return $"біт {i} = {(t.IsEven ? 0 : 1)}: результат = {result}, основа² = {x * x % m}";
        x = x * x % m;
    }
}
