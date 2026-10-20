using System.Text.Json;

namespace Agender.Infrastructure.Google;

public sealed record ServiceAccountKey(string ClientEmail, string PrivateKey)
{
    public const int MaxJsonLength = 20_000;

    private const string ServiceAccountType = "service_account";

    public static ServiceAccountKey Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonLength)
        {
            throw new ArgumentException("The service account key is missing or too large.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || ReadString(root, "type") != ServiceAccountType
                || ReadString(root, "client_email") is not { Length: > 0 } email
                || ReadString(root, "private_key") is not { Length: > 0 } privateKey)
            {
                throw new ArgumentException("The key is not a valid Google service account key.");
            }

            return new ServiceAccountKey(email, privateKey);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The service account key is not valid JSON.", ex);
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
