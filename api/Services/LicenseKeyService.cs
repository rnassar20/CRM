using System.Security.Cryptography;
using System.Text;

namespace Crm.Api.Services;

public interface ILicenseKeyService
{
    /// <summary>Generates a signed, encrypted activation key for a subscription.</summary>
    string GenerateKey(int clientId, int subscriptionId, DateTime expiryDate);
    /// <summary>Decrypts/validates a key and returns (clientId, subscriptionId, expiryDate). Throws on tamper.</summary>
    (int ClientId, int SubscriptionId, DateTime ExpiryDate) ParseKey(string key);
    string HashKey(string key);
}

/// <summary>
/// Key format: Base32( IV(16) || AES-256-CBC(payload) || HMAC-SHA256(iv+payload) ) grouped in 5-char blocks.
/// Payload: "CRM|{clientId}|{subscriptionId}|{expiry:yyyyMMdd}"
/// AES + MAC keys are derived from one master secret. Chosen over AES-GCM so the legacy
/// .NET Framework VB.NET ERP can decrypt with plain AesManaged/HMACSHA256 (see README).
/// </summary>
public class LicenseKeyService(IConfiguration config) : ILicenseKeyService
{
    private const char Sep = '-';
    private readonly byte[] _master = Encoding.UTF8.GetBytes(config["Licensing:Secret"] ?? throw new InvalidOperationException("Licensing:Secret not configured"));

    private (byte[] aesKey, byte[] macKey) Derive()
    {
        var aesKey = SHA256.HashData([.. _master, .. Encoding.ASCII.GetBytes("|aes")]);
        var macKey = SHA256.HashData([.. _master, .. Encoding.ASCII.GetBytes("|mac")]);
        return (aesKey, macKey);
    }

    public string GenerateKey(int clientId, int subscriptionId, DateTime expiryDate)
        => Format(Base32Encode(Protect($"CRM|{clientId}|{subscriptionId}|{expiryDate:yyyyMMdd}")));

    public (int, int, DateTime) ParseKey(string key)
    {
        var payload = Unprotect(Base32Decode(Unformat(key)));
        var parts = payload.Split('|');
        if (parts.Length != 4 || parts[0] != "CRM")
            throw new InvalidOperationException("Malformed license payload");
        return (int.Parse(parts[1]), int.Parse(parts[2]), DateTime.ParseExact(parts[3], "yyyyMMdd", null));
    }

    public string HashKey(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(key))));

    private static string Normalize(string key) => key.Trim().Replace(" ", "").Replace(Sep, '\0').Replace("\0", "").ToUpperInvariant();

    private byte[] Protect(string payload)
    {
        var (aesKey, macKey) = Derive();
        using var aes = Aes.Create();
        aes.Key = aesKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var cipher = enc.TransformFinalBlock(Encoding.UTF8.GetBytes(payload), 0, Encoding.UTF8.GetByteCount(payload));
        var data = new byte[aes.IV.Length + cipher.Length];
        aes.IV.CopyTo(data, 0);
        cipher.CopyTo(data, aes.IV.Length);
        using var hmac = new HMACSHA256(macKey);
        return [.. data, .. hmac.ComputeHash(data)];
    }

    private string Unprotect(byte[] blob)
    {
        if (blob.Length < 16 + 32) throw new InvalidOperationException("Key too short");
        var (aesKey, macKey) = Derive();
        var data = blob[..^32];
        var mac = blob[^32..];
        using var hmac = new HMACSHA256(macKey);
        if (!CryptographicOperations.FixedTimeEquals(hmac.ComputeHash(data), mac))
            throw new InvalidOperationException("License key integrity check failed");
        using var aes = Aes.Create();
        aes.Key = aesKey;
        using var dec = aes.CreateDecryptor(aesKey, data[..16]);
        var cipher = data[16..];
        var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }

    private static string Format(string encoded) => string.Join(Sep, Enumerable.Range(0, (int)Math.Ceiling(encoded.Length / 5.0))
            .Select(i => i * 5 >= encoded.Length ? "" : encoded.Substring(i * 5, Math.Min(5, encoded.Length - i * 5))));

    private static string Unformat(string key) => Normalize(key);

    private static readonly char[] Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    private static string Base32Encode(byte[] bytes)
    {
        var sb = new StringBuilder((bytes.Length * 8 + 4) / 5);
        int bitBuffer = 0, bits = 0;
        foreach (var b in bytes)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(bitBuffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(Alphabet[(bitBuffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string s)
    {
        int bitBuffer = 0, bits = 0;
        var output = new List<byte>(s.Length * 5 / 8);
        foreach (var c in s.ToUpperInvariant())
        {
            var val = Array.IndexOf(Alphabet, c);
            if (val < 0) throw new InvalidOperationException($"Invalid character '{c}' in license key");
            bitBuffer = (bitBuffer << 5) | val;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((bitBuffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return [.. output];
    }
}
