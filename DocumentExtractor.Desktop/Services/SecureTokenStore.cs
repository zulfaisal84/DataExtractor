using System.Security.Cryptography;
using System.Text;

namespace DocumentExtractor.Desktop.Services;

/// <summary>
/// Stores JWT tokens encrypted on disk using OS-provided protection (DPAPI on Windows, gcrypt on Linux, Keychain on macOS).
/// </summary>
public class SecureTokenStore
{
    private readonly string _filePath;

    public SecureTokenStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "DocumentExtractor");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "token.dat");
    }

    public void Save(string accessToken, string refreshToken)
    {
        var json = $"{accessToken}|||{refreshToken}";
        var data = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encrypted);
    }

    public (string? access, string? refresh) LoadTokens()
    {
        if (!File.Exists(_filePath)) return (null, null);
        try
        {
            var encrypted = File.ReadAllBytes(_filePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decrypted);
            var parts = json.Split("|||", 2);
            if (parts.Length == 2)
                return (parts[0], parts[1]);
        }
        catch { }
        return (null, null);
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}