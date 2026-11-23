namespace Rsa.Web;

// Моделі запитів/відповідей API. Усі великі числа передаються як hex-рядки.

/// <summary>Число у двох поданнях.</summary>
public record NumberView(string Hex, string Dec);

public record KeyRequest(int Bits, int E);

public record KeyResponse(
    int Bits, NumberView P, NumberView Q, NumberView N, NumberView Phi,
    NumberView E, NumberView D, string Check, bool CheckOk, int BlockBytes, long ElapsedMs);

public record SmallDemoRequest(string P, string Q, string E, string M);

public record SmallDemoResponse(string[] Steps, string N, string Phi, string D, string C, string Decrypted);

public record PublicKeyDto(string E, string N);

public record EncryptRequest(string Text, string E, string N);

/// <summary>Один блок: байти відкритого тексту, Mᵢ та Cᵢ.</summary>
public record BlockView(int Index, string BytesHex, string MDec, string MHex, string CHex);

public record EncryptResponse(int BlockBytes, BlockView[] Blocks);

public record DecryptRequest(string[] CipherBlocks, string D, string N);

public record DecryptResponse(BlockView[] Blocks, string Text);

public record ServerKeyResponse(int Bits, string E, string N, int BlockBytes);

public record ExchangeRequest(string[] CiphertextBlocks, PublicKeyDto ClientPublicKey);

/// <summary>Що побачив сервер після розшифрування (повертається лише для журналу демонстрації).</summary>
public record ServerView(string[] DecryptedBlocksHex, string DecryptedText, string ReplyText);

public record ExchangeResponse(string[] ReplyBlocks, ServerView ServerLog);
