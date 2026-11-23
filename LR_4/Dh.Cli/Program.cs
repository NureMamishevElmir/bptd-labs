using System.Numerics;
using System.Text;
using Dh.Core;

// Демонстрація: три користувачі B, C, D попарно обмінюються ключами.
Console.OutputEncoding = Encoding.UTF8;
bool full = args.Contains("--full"); // повні 2048-бітні числа

// 1. Навчальний режим: Q = 23, A = 5
var small = DhGroup.Small;
Console.WriteLine($"=== {small.Name} ===");
Console.WriteLine($"Степені A^k mod Q, k = 1..{small.Q - 1}:");
Console.WriteLine("  " + string.Join(" ", ModMath.Powers((int)small.A, (int)small.Q)));
Console.WriteLine($"  5 первісний корінь за модулем 23: {ModMath.IsPrimitiveRootBrute(5, 23)}");
Console.WriteLine($"  2 первісний корінь за модулем 23: {ModMath.IsPrimitiveRootBrute(2, 23)}" +
                  $"  (2^k: {string.Join(" ", ModMath.Powers(2, 23))})");
Run(new[] { new DhParty("B", small, 6), new DhParty("C", small, 15), new DhParty("D", small, 13) }, n => n.ToString());

// 2. Реальні параметри: RFC 3526, 2048 біт
var big = DhGroup.Rfc3526;
Console.WriteLine();
Console.WriteLine($"=== {big.Name}, {big.Bits} біт ===");
Console.WriteLine($"Q просте: {ModMath.IsProbablePrime(big.Q)}, (Q-1)/2 просте: {ModMath.IsProbablePrime((big.Q - 1) / 2)}");
Console.WriteLine($"2 первісний корінь: {ModMath.IsPrimitiveRootSafePrime(2, big.Q)}" +
                  $" (2^((Q-1)/2) mod Q = {ModMath.ModPow(2, (big.Q - 1) / 2, big.Q)})");
Console.WriteLine($"Найменший первісний корінь A = {big.A}");
Run(new[] { new DhParty("B", big), new DhParty("C", big), new DhParty("D", big) }, n => Short(n, full));

// 3. Атака "людина посередині": сервер M підміняє Y між B і C
Console.WriteLine();
Console.WriteLine("=== MITM (навчальна група): M підміняє ключі між B і C ===");
var b = new DhParty("B", small, 6); var c = new DhParty("C", small, 15); var m = new DhParty("M", small, 9);
Console.WriteLine($"B отримує Y_M={m.Y} замість Y_C={c.Y}: K_B = {b.SharedKey(m)}, M знає K = {m.SharedKey(b)}");
Console.WriteLine($"C отримує Y_M={m.Y} замість Y_B={b.Y}: K_C = {c.SharedKey(m)}, M знає K = {m.SharedKey(c)}");
Console.WriteLine($"Відбитки: B {DhParty.Fingerprint(b.SharedKey(m))} != C {DhParty.Fingerprint(c.SharedKey(m))}" +
                  " -> порівняння відбитків виявляє атаку");

static void Run(DhParty[] users, Func<BigInteger, string> fmt)
{
    var g = users[0].Group;
    Console.WriteLine($"Q = {fmt(g.Q)}, A = {g.A}");
    foreach (var u in users)
        Console.WriteLine($"  {u.Name}: X = {fmt(u.X)}, Y = A^X mod Q = {fmt(u.Y)}");
    for (int i = 0; i < users.Length; i++)
        for (int j = i + 1; j < users.Length; j++)
        {
            var (p, q) = (users[i], users[j]);
            BigInteger kp = p.SharedKey(q), kq = q.SharedKey(p);
            Console.WriteLine($"  {p.Name}-{q.Name}: K = Y_{q.Name}^X_{p.Name} = {fmt(kp)}; " +
                              $"K = Y_{p.Name}^X_{q.Name} = {fmt(kq)}; рівні: {kp == kq}; відбиток {DhParty.Fingerprint(kp)}");
        }
}

// Скорочений запис великого числа: перші та останні 12 hex-символів
static string Short(BigInteger n, bool full)
{
    string h = Convert.ToHexString(n.ToByteArray(isUnsigned: true, isBigEndian: true)).ToLowerInvariant();
    return full || h.Length <= 28 ? "0x" + h : $"0x{h[..12]}…{h[^12..]} ({n.GetBitLength()} біт)";
}
