using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using DocumentExtractor.Desktop.Models;

namespace DocumentExtractor.Desktop.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly SecureTokenStore _store = new();
    private string? _jwtToken;

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // Attempt to load previously saved token
        var token = _store.Load();
        if (!string.IsNullOrEmpty(token))
        {
            _jwtToken = token;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { Email = email, Password = password });
        if (!response.IsSuccessStatusCode)
            return false;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (json.TryGetProperty("token", out var tokenElement))
        {
            _jwtToken = tokenElement.GetString();
            if (!string.IsNullOrEmpty(_jwtToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
                _store.Save(_jwtToken);
                return true;
            }
        }
        return false;
    }

    public void Logout()
    {
        _jwtToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _store.Clear();
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_jwtToken);
}