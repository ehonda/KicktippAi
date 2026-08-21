using System.Security.Cryptography;
using System.Text;

namespace OpenAiIntegration;

public static class PromptTemplateContentHash
{
    public static string ComputeSha256(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd() + "\n";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(bytes);
    }
}
