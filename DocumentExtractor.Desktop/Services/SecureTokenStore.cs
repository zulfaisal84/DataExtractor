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

    public void Save(string token)
    {
        var data = Encoding.UTF8.GetBytes(token);
        var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encrypted);
    }

    public string? Load()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(_filePath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null; // corruption or wrong user scope
        }
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}