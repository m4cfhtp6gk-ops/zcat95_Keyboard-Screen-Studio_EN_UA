using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KeyboardScreen.Core;

/// <summary>
/// Keeps the signed-in Claude account on disk between runs.
///
/// This is the one place the app holds a credential of its own rather than
/// borrowing Claude Code's, so it is also the one place a token is written
/// down. On Windows it is sealed with DPAPI under the current user, so another
/// account on the same machine cannot read it and it does not survive being
/// copied to a different machine. It lives in its own file, never in
/// settings.json, so an exported or synced settings backup never carries it.
///
/// Off Windows there is no DPAPI; the fallback writes plaintext, which is only
/// ever reached by the tests - the app itself ships on Windows.
/// </summary>
public sealed class ClaudeOAuthStore
{
    private readonly string _path;

    public ClaudeOAuthStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KeyboardScreenStudio",
        "claude-oauth.bin");

    /// <summary>Whether a sign-in is stored at all, without decrypting it.</summary>
    public bool HasTokens => File.Exists(_path);

    public ClaudeOAuthTokens? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            byte[] sealedBytes = File.ReadAllBytes(_path);
            byte[] plain = Unprotect(sealedBytes);
            using JsonDocument document = JsonDocument.Parse(plain);
            JsonElement root = document.RootElement;

            string access = root.GetProperty("access_token").GetString() ?? string.Empty;
            string refresh = root.TryGetProperty("refresh_token", out JsonElement r)
                ? r.GetString() ?? string.Empty
                : string.Empty;
            DateTimeOffset expires = root.TryGetProperty("expires_at", out JsonElement e)
                && e.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(e.GetString(), out DateTimeOffset parsed)
                    ? parsed
                    : DateTimeOffset.MinValue;

            return access.Length == 0 ? null : new ClaudeOAuthTokens(access, refresh, expires);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or JsonException or CryptographicException or FormatException)
        {
            // A blob that cannot be read - corrupted, or sealed by a different
            // user - is treated as no login rather than a crash. Signing in
            // again overwrites it.
            return null;
        }
    }

    public void Save(ClaudeOAuthTokens tokens)
    {
        var payload = new
        {
            access_token = tokens.AccessToken,
            refresh_token = tokens.RefreshToken,
            expires_at = tokens.ExpiresAt.ToString("o")
        };
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(payload);

        string directory = Path.GetDirectoryName(_path) ?? string.Empty;
        if (directory.Length > 0)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(_path, Protect(plain));
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a file we cannot delete is one the user will have to
            // remove by hand, but there is nothing to throw about here.
        }
    }

    // A four-byte marker distinguishes a DPAPI blob from the plaintext fallback,
    // so a file written on one platform is not mistaken for the other.
    private static readonly byte[] DpapiMarker = "DPA1"u8.ToArray();
    private static readonly byte[] PlainMarker = "RAW1"u8.ToArray();

    private static byte[] Protect(byte[] plain)
    {
        if (OperatingSystem.IsWindows())
        {
            byte[] sealed_ = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            return Concat(DpapiMarker, sealed_);
        }

        return Concat(PlainMarker, plain);
    }

    private static byte[] Unprotect(byte[] stored)
    {
        if (stored.Length < 4)
        {
            throw new FormatException("credential file is too short to be valid");
        }

        byte[] marker = stored[..4];
        byte[] rest = stored[4..];

        if (marker.SequenceEqual(DpapiMarker))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new CryptographicException("a DPAPI credential cannot be read off Windows");
            }

            return ProtectedData.Unprotect(rest, null, DataProtectionScope.CurrentUser);
        }

        if (marker.SequenceEqual(PlainMarker))
        {
            return rest;
        }

        throw new FormatException("unrecognized credential file format");
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length);
        Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
        return result;
    }
}
