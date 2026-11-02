using System.Security.Cryptography;
using System.Text;

namespace Agender.Infrastructure.Trader;

public class TraderKey
{
    public const int HashLength = 64;
    public const string Prefix = "agt_";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastSyncAt { get; set; }

    public static string Generate() => Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim()))).ToLowerInvariant();
}
