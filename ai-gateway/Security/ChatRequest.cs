using System.Security.Cryptography;
using System.Text;
namespace AiGateway.Security;
public static class ChatRequest
{
    public static string Fingerprint(params string?[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(parts))));
}
