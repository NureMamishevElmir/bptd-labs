// rsa.js — власна реалізація RSA на BigInt для браузера (клієнтська сторона).
// Та сама схема, що й на сервері: UTF-8 байти → блоки по k−1 байт (k — довжина N у байтах).
(function (root) {
  "use strict";

  // Невід'ємний залишок a mod m
  function mod(a, m) {
    const r = a % m;
    return r < 0n ? r + m : r;
  }

  // НСД за алгоритмом Евкліда
  function gcd(a, b) {
    a = a < 0n ? -a : a;
    b = b < 0n ? -b : b;
    while (b !== 0n) [a, b] = [b, a % b];
    return a;
  }

  // Розширений Евклід: [g, x, y], де a·x + b·y = g
  function egcd(a, b) {
    let [oldR, r, oldX, x, oldY, y] = [a, b, 1n, 0n, 0n, 1n];
    while (r !== 0n) {
      const q = oldR / r;
      [oldR, r] = [r, oldR - q * r];
      [oldX, x] = [x, oldX - q * x];
      [oldY, y] = [y, oldY - q * y];
    }
    return [oldR, oldX, oldY];
  }

  // Обернений елемент a⁻¹ mod m
  function modInverse(a, m) {
    const [g, x] = egcd(mod(a, m), m);
    if (g !== 1n) throw new Error("Обернений елемент не існує");
    return mod(x, m);
  }

  // b^e mod m методом «квадрат і множення»
  function modPow(b, e, m) {
    if (m === 1n) return 0n;
    let result = 1n;
    b = mod(b, m);
    while (e > 0n) {
      if (e & 1n) result = (result * b) % m;
      b = (b * b) % m;
      e >>= 1n;
    }
    return result;
  }

  // ---- Перетворення чисел ----
  const bitLength = (x) => (x === 0n ? 0 : x.toString(2).length);
  const byteLength = (x) => Math.ceil(bitLength(x) / 8);
  const toHex = (x) => x.toString(16).toUpperCase();
  const fromHex = (h) => BigInt("0x" + h.trim().replace(/^0x/i, ""));

  function bytesToBig(bytes) {
    let x = 0n;
    for (const b of bytes) x = (x << 8n) | BigInt(b);
    return x;
  }

  // Число → байти big-endian (мінімальна довжина або доповнення нулями до len)
  function bigToBytes(x, len = 0) {
    const out = [];
    while (x > 0n) { out.unshift(Number(x & 0xFFn)); x >>= 8n; }
    while (out.length < len) out.unshift(0);
    return Uint8Array.from(out);
  }

  const bytesToHex = (bytes) => Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("").toUpperCase();

  // ---- Випадкові та прості числа ----
  function randomBytes(n) {
    const a = new Uint8Array(n);
    root.crypto.getRandomValues(a);
    return a;
  }

  // Випадкове непарне число рівно з bits біт (два старші біти = 1)
  function randomOddWithBits(bits) {
    const bytes = randomBytes(Math.ceil(bits / 8));
    const extra = bytes.length * 8 - bits;
    bytes[0] &= 0xFF >> extra;
    bytes[0] |= 0xC0 >> extra;
    bytes[bytes.length - 1] |= 1;
    return bytesToBig(bytes);
  }

  // Рівномірне випадкове число з [min, max] (метод відкидання)
  function randomInRange(min, max) {
    const range = max - min;
    const bits = bitLength(range);
    if (bits === 0) return min;
    const len = Math.ceil(bits / 8);
    const mask = 0xFF >> (len * 8 - bits);
    for (;;) {
      const bytes = randomBytes(len);
      bytes[0] &= mask;
      const r = bytesToBig(bytes);
      if (r <= range) return min + r;
    }
  }

  // Малі прості до 1000 для пробного ділення
  const SMALL_PRIMES = (() => {
    const out = [], sieve = new Uint8Array(1001);
    for (let i = 2; i <= 1000; i++) {
      if (sieve[i]) continue;
      out.push(BigInt(i));
      for (let j = i * i; j <= 1000; j += i) sieve[j] = 1;
    }
    return out;
  })();

  // Тест Міллера–Рабіна
  function isProbablePrime(n, rounds = 40) {
    if (n < 2n) return false;
    for (const p of SMALL_PRIMES) {
      if (n === p) return true;
      if (n % p === 0n) return false;
    }
    let d = n - 1n, s = 0;
    while ((d & 1n) === 0n) { d >>= 1n; s++; }
    for (let i = 0; i < rounds; i++) {
      const a = randomInRange(2n, n - 2n);
      let x = modPow(a, d, n);
      if (x === 1n || x === n - 1n) continue;
      let witness = true;
      for (let r = 1; r < s; r++) {
        x = (x * x) % n;
        if (x === n - 1n) { witness = false; break; }
      }
      if (witness) return false;
    }
    return true;
  }

  function generatePrime(bits) {
    for (;;) {
      const c = randomOddWithBits(bits);
      if (isProbablePrime(c)) return c;
    }
  }

  // Генерація пари ключів: модуль N рівно bits біт
  function generateKeyPair(bits, e = 65537n) {
    e = BigInt(e);
    const pBits = Math.floor(bits / 2), qBits = bits - pBits;
    for (;;) {
      const p = generatePrime(pBits), q = generatePrime(qBits);
      if (p === q || gcd(e, p - 1n) !== 1n || gcd(e, q - 1n) !== 1n) continue;
      const n = p * q, phi = (p - 1n) * (q - 1n);
      const d = modInverse(e, phi);
      return { p, q, n, phi, e, d, publicKey: { e, n }, privateKey: { d, n } };
    }
  }

  // ---- Шифрування тексту ----
  function blockBytes(n) {
    const size = byteLength(n) - 1;
    if (size < 1) throw new Error("N замалий");
    return size;
  }

  // Текст → масив блоків-байтів по k−1 байт
  function splitBytes(text, n) {
    const data = new TextEncoder().encode(text), size = blockBytes(n), out = [];
    for (let i = 0; i < data.length; i += size) out.push(data.slice(i, i + size));
    return out;
  }

  const textToBlocks = (text, n) => splitBytes(text, n).map(bytesToBig);

  // Блоки Mᵢ → текст (усі, крім останнього, доповнюємо до k−1 байт)
  function blocksToText(blocks, n) {
    const size = blockBytes(n), parts = [];
    blocks.forEach((m, i) => parts.push(...bigToBytes(m, i < blocks.length - 1 ? size : 0)));
    return new TextDecoder().decode(Uint8Array.from(parts));
  }

  const encrypt = (text, { e, n }) => textToBlocks(text, n).map((m) => modPow(m, e, n));
  const decrypt = (blocks, { d, n }) => blocksToText(blocks.map((c) => modPow(c, d, n)), n);

  const Rsa = {
    mod, gcd, egcd, modInverse, modPow, isProbablePrime, generatePrime, generateKeyPair,
    bitLength, byteLength, toHex, fromHex, bytesToHex, bytesToBig, bigToBytes,
    blockBytes, splitBytes, textToBlocks, blocksToText, encrypt, decrypt,
  };

  if (typeof module !== "undefined" && module.exports) module.exports = Rsa;
  else root.Rsa = Rsa;
})(typeof globalThis !== "undefined" ? globalThis : this);
