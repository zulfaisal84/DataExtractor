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
    private string? _refreshToken;

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // Attempt to load previously saved token
        var (access, refresh) = _store.LoadTokens();
        if (!string.IsNullOrEmpty(access))
        {
            _jwtToken = access;
            _refreshToken = refresh;
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
                _refreshToken = json.GetProperty("refreshToken").GetString();
                _store.Save(_jwtToken!, _refreshToken!);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
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

    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken) || string.IsNullOrEmpty(_jwtToken))
            return false;

        var response = await _httpClient.PostAsJsonAsync("api/auth/refresh", new { AccessToken = _jwtToken, RefreshToken = _refreshToken });
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        _jwtToken = json.GetProperty("accessToken").GetString();
        _refreshToken = json.GetProperty("refreshToken").GetString();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
        _store.Save(_jwtToken!, _refreshToken!);
        return true;
    }
}