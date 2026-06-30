using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace TextCrate;

internal sealed record LongTextRelayOptions(
    string Endpoint,
    int ExpiryMinutes,
    bool BurnAfterRead,
    string? Password);

internal sealed record LongTextRelayResult(string Url, string PowerShellHelper);

internal static class LongTextRelayService
{
    private const int TokenBytes = 24;
    private const int KeyBytes = 32;
    private const int NonceBytes = 12;
    private const int SaltBytes = 16;
    private const int TagBytes = 16;
    private const int MaxPlainTextBytes = 192 * 1024;
    private const int Pbkdf2Iterations = 310000;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static bool ShouldOffer(string text, AppSettings settings)
    {
        if (!settings.LongTextRelayEnabled || string.IsNullOrWhiteSpace(settings.LongTextRelayEndpoint))
        {
            return false;
        }

        return text.Length >= Math.Clamp(settings.LongTextRelayOfferOver, 250, 1000000)
            || text.Count(static c => c == '\n') >= 40
            || text.Length > 1200 && text.Any(static c => c is '\t' or '{' or '}' or '<' or '>' or '\\');
    }

    public static async Task<LongTextRelayResult> CreateAsync(string text, LongTextRelayOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException("Long Text Relay endpoint is not configured.");
        }

        var plaintext = Encoding.UTF8.GetBytes(text);
        if (plaintext.Length > MaxPlainTextBytes)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new InvalidOperationException($"Text is too large for Long Text Relay. Limit is {MaxPlainTextBytes / 1024} KiB.");
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var masterKey = RandomNumberGenerator.GetBytes(KeyBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var burnToken = RandomNumberGenerator.GetBytes(KeyBytes);
        var passwordProtected = !string.IsNullOrEmpty(options.Password);
        var salt = passwordProtected ? RandomNumberGenerator.GetBytes(SaltBytes) : Array.Empty<byte>();
        var finalKey = DeriveContentKey(masterKey, salt, options.Password);
        var ciphertext = new byte[plaintext.Length + TagBytes];

        try
        {
            // AES-GCM gives confidentiality and integrity. The Worker receives only
            // ciphertext, nonce, salt, expiry metadata, and random opaque tokens.
            using var aes = new AesGcm(finalKey, TagBytes);
            aes.Encrypt(nonce, plaintext, ciphertext.AsSpan(0, plaintext.Length), ciphertext.AsSpan(plaintext.Length, TagBytes));

            var token = Base64Url.Encode(tokenBytes);
            var endpoint = NormalizeEndpoint(options.Endpoint);
            var payload = new UploadRequest(
                token,
                Base64Url.Encode(ciphertext),
                Base64Url.Encode(nonce),
                passwordProtected ? Base64Url.Encode(salt) : null,
                Math.Clamp(options.ExpiryMinutes, 1, 60),
                options.BurnAfterRead,
                passwordProtected,
                Pbkdf2Iterations,
                Base64Url.Encode(burnToken));

            using var response = await Http.PostAsJsonAsync($"{endpoint}/.gk7/store", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(response.StatusCode == HttpStatusCode.TooManyRequests
                    ? "Long Text Relay is rate limited. Try again later."
                    : "Long Text Relay upload failed.");
            }

            var keyFragment = $"k={Uri.EscapeDataString(Base64Url.Encode(masterKey))}&b={Uri.EscapeDataString(Base64Url.Encode(burnToken))}";
            if (passwordProtected)
            {
                keyFragment += "&p=1";
            }

            var relayUrl = $"{endpoint}/x/{token}#{keyFragment}";
            var helper = $"Open the link and press Copy full text. PowerShell cannot decrypt this with irm alone because the key is kept in the URL fragment and is never sent to Cloudflare.";
            return new LongTextRelayResult(relayUrl, helper);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(masterKey);
            CryptographicOperations.ZeroMemory(finalKey);
            CryptographicOperations.ZeroMemory(tokenBytes);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(burnToken);
            if (salt.Length > 0)
            {
                CryptographicOperations.ZeroMemory(salt);
            }
        }
    }

    public static async Task TestConnectionAsync(string endpoint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Enter a backend endpoint first.");
        }

        using var response = await Http.GetAsync($"{NormalizeEndpoint(endpoint)}/.gk7/status", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Long Text Relay backend did not respond successfully.");
        }
    }

    public static byte[] DeriveContentKey(byte[] masterKey, byte[] salt, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return (byte[])masterKey.Clone();
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
        var passwordKey = pbkdf2.GetBytes(KeyBytes);
        try
        {
            var combined = new byte[masterKey.Length + passwordKey.Length];
            Buffer.BlockCopy(masterKey, 0, combined, 0, masterKey.Length);
            Buffer.BlockCopy(passwordKey, 0, combined, masterKey.Length, passwordKey.Length);
            try
            {
                return SHA256.HashData(combined);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(combined);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordKey);
        }
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        return endpoint.Trim().TrimEnd('/');
    }

    private sealed record UploadRequest(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("ciphertext")] string Ciphertext,
        [property: JsonPropertyName("nonce")] string Nonce,
        [property: JsonPropertyName("salt")] string? Salt,
        [property: JsonPropertyName("expiryMinutes")] int ExpiryMinutes,
        [property: JsonPropertyName("burnAfterRead")] bool BurnAfterRead,
        [property: JsonPropertyName("passwordProtected")] bool PasswordProtected,
        [property: JsonPropertyName("kdfIterations")] int KdfIterations,
        [property: JsonPropertyName("burnToken")] string BurnToken);
}
