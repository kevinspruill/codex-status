using System.Text.RegularExpressions;

namespace CodexStatus.Core;

public static partial class SecretRedactor
{
    public const string Redacted = "[REDACTED]";

    public static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var result = value;
        result = OpenAiKeyRegex().Replace(result, $"$1{Redacted}");
        result = GithubTokenRegex().Replace(result, Redacted);
        result = AwsAccessKeyRegex().Replace(result, Redacted);
        result = BearerTokenRegex().Replace(result, $"$1 {Redacted}");
        result = AuthorizationHeaderRegex().Replace(result, $"$1: {Redacted}");
        result = PasswordFlagRegex().Replace(result, $"$1 {Redacted}");
        result = KeyValueSecretRegex().Replace(result, $"$1={Redacted}");
        return result;
    }

    [GeneratedRegex(@"(?i)\b(OPENAI_API_KEY\s*=\s*)([A-Za-z0-9_\-]{20,})\b")]
    private static partial Regex OpenAiKeyRegex();

    [GeneratedRegex(@"\b(ghp|gho|ghu|ghs|ghr|github_pat)_[A-Za-z0-9_]{20,}\b")]
    private static partial Regex GithubTokenRegex();

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b")]
    private static partial Regex AwsAccessKeyRegex();

    [GeneratedRegex(@"(?i)\b(Bearer)\s+[A-Za-z0-9_\-./+=]{16,}")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?i)\b(Authorization)\s*:\s*[A-Za-z]+\s+[A-Za-z0-9_\-./+=]{16,}")]
    private static partial Regex AuthorizationHeaderRegex();

    [GeneratedRegex(@"(?i)(--password(?:=|\s+))\S+")]
    private static partial Regex PasswordFlagRegex();

    [GeneratedRegex(@"(?i)\b(password|token|api[_-]?key|secret)\s*=\s*[^&\s;]+")]
    private static partial Regex KeyValueSecretRegex();
}
