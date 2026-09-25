using System.Security.Cryptography;

namespace Domio.Infrastructure.Notifications;

public sealed class NotificationSecretProtector
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly string keyPath;
    private readonly object sync = new();
    private byte[]? cachedKey;

    public NotificationSecretProtector(
        string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(
                contentRootPath))
        {
            throw new ArgumentException(
                "Brak katalogu aplikacji.",
                nameof(contentRootPath));
        }

        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var fullContentRoot =
            Path.GetFullPath(
                contentRootPath);

        var fullTempRoot =
            Path.GetFullPath(
                Path.GetTempPath());

        var isTemporaryRuntime =
            fullContentRoot.StartsWith(
                fullTempRoot,
                StringComparison.OrdinalIgnoreCase);

        var secretDirectory =
            isTemporaryRuntime ||
            string.IsNullOrWhiteSpace(
                localApplicationData)
                ? Path.Combine(
                    contentRootPath,
                    ".domio-secrets")
                : Path.Combine(
                    localApplicationData,
                    "Domio",
                    "secrets");

        keyPath =
            Path.Combine(
                secretDirectory,
                "notification-mail.key");
    }

    public string Protect(
        string plainText)
    {
        if (string.IsNullOrEmpty(
                plainText))
        {
            return string.Empty;
        }

        var key =
            GetOrCreateKey();

        var nonce =
            RandomNumberGenerator.GetBytes(
                NonceSize);

        var plainBytes =
            System.Text.Encoding.UTF8.GetBytes(
                plainText);

        var cipher =
            new byte[plainBytes.Length];

        var tag =
            new byte[TagSize];

        using var aes =
            new AesGcm(
                key,
                TagSize);

        aes.Encrypt(
            nonce,
            plainBytes,
            cipher,
            tag);

        var payload =
            new byte[
                1 +
                NonceSize +
                TagSize +
                cipher.Length];

        payload[0] = 1;

        Buffer.BlockCopy(
            nonce,
            0,
            payload,
            1,
            NonceSize);

        Buffer.BlockCopy(
            tag,
            0,
            payload,
            1 + NonceSize,
            TagSize);

        Buffer.BlockCopy(
            cipher,
            0,
            payload,
            1 + NonceSize + TagSize,
            cipher.Length);

        return Convert.ToBase64String(
            payload);
    }

    public string Unprotect(
        string protectedText)
    {
        if (string.IsNullOrWhiteSpace(
                protectedText))
        {
            return string.Empty;
        }

        var payload =
            Convert.FromBase64String(
                protectedText);

        if (payload.Length <
                1 +
                NonceSize +
                TagSize ||
            payload[0] != 1)
        {
            throw new CryptographicException(
                "Nie można odczytać zaszyfrowanego hasła SMTP.");
        }

        var key =
            GetOrCreateKey();

        var nonce =
            payload
                .AsSpan(
                    1,
                    NonceSize)
                .ToArray();

        var tag =
            payload
                .AsSpan(
                    1 + NonceSize,
                    TagSize)
                .ToArray();

        var cipher =
            payload
                .AsSpan(
                    1 +
                    NonceSize +
                    TagSize)
                .ToArray();

        var plain =
            new byte[cipher.Length];

        using var aes =
            new AesGcm(
                key,
                TagSize);

        aes.Decrypt(
            nonce,
            cipher,
            tag,
            plain);

        return System.Text.Encoding.UTF8
            .GetString(
                plain);
    }

    private byte[] GetOrCreateKey()
    {
        lock (sync)
        {
            if (cachedKey is not null)
            {
                return cachedKey;
            }

            var directory =
                Path.GetDirectoryName(
                    keyPath)
                ?? throw new InvalidOperationException(
                    "Nie można ustalić katalogu klucza.");

            Directory.CreateDirectory(
                directory);

            if (File.Exists(
                    keyPath))
            {
                var existing =
                    File.ReadAllBytes(
                        keyPath);

                if (existing.Length !=
                    KeySize)
                {
                    throw new InvalidOperationException(
                        "Lokalny klucz SMTP Domio ma nieprawidłowy format.");
                }

                cachedKey = existing;
                return cachedKey;
            }

            var key =
                RandomNumberGenerator.GetBytes(
                    KeySize);

            File.WriteAllBytes(
                keyPath,
                key);

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    File.SetAttributes(
                        keyPath,
                        File.GetAttributes(
                            keyPath) |
                        FileAttributes.Hidden);
                }
            }
            catch
            {
                // Ukrycie pliku jest tylko dodatkowym zabezpieczeniem.
            }

            cachedKey = key;
            return cachedKey;
        }
    }
}
