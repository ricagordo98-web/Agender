using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agender.Shared.Contracts;

namespace Agender.Auth;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    InvalidServerAddress,
    ServerUnreachable
}

public sealed class AuthSession
{
    private const string ServerKey = "agender.server";
    private const string AccessKey = "agender.access";
    private const string RefreshKey = "agender.refresh";
    private const string ExpiresKey = "agender.expires";
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset _expiresAt;
    private bool _initialized;

    public event Action? Changed;

    public string ServerUrl { get; private set; } = Preferences.Default.Get(ServerKey, DefaultServerUrl);

    public bool IsAuthenticated => !string.IsNullOrEmpty(_refreshToken);

    public UserInfoDto? User { get; private set; }

    private static string DefaultServerUrl =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5018" : "http://localhost:5018";

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            _accessToken = await ReadSecureAsync(AccessKey);
            _refreshToken = await ReadSecureAsync(RefreshKey);
            _expiresAt = long.TryParse(await ReadSecureAsync(ExpiresKey), out long unix) ? DateTimeOffset.FromUnixTimeSeconds(unix) : DateTimeOffset.MinValue;
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }

        if (IsAuthenticated)
        {
            await LoadUserAsync();
        }
    }

    public async Task<LoginResult> LoginAsync(string serverUrl, string email, string password)
    {
        if (!TryNormalizeServer(serverUrl, out string normalized))
        {
            return LoginResult.InvalidServerAddress;
        }

        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync($"{normalized}/api/auth/login", new { email, password });

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                return LoginResult.InvalidCredentials;
            }

            if (!response.IsSuccessStatusCode)
            {
                return LoginResult.InvalidServerAddress;
            }

            TokenResponse? tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
            if (tokens is null)
            {
                return LoginResult.InvalidServerAddress;
            }

            ServerUrl = normalized;
            Preferences.Default.Set(ServerKey, normalized);
            await StoreAsync(tokens);
            _initialized = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return LoginResult.ServerUnreachable;
        }

        await LoadUserAsync();
        Changed?.Invoke();
        return LoginResult.Success;
    }

    public async Task LogoutAsync()
    {
        await ClearAsync();
        Changed?.Invoke();
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        await InitializeAsync();

        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _expiresAt - RefreshMargin)
        {
            return _accessToken;
        }

        return await RefreshAsync(usedToken: null) ? _accessToken : null;
    }

    public Task<bool> RefreshAfterUnauthorizedAsync(string? usedToken) => RefreshAsync(usedToken);

    private async Task<bool> RefreshAsync(string? usedToken)
    {
        await _gate.WaitAsync();
        try
        {
            if (usedToken is not null && _accessToken is not null && usedToken != _accessToken)
            {
                return true;
            }

            if (string.IsNullOrEmpty(_refreshToken))
            {
                return false;
            }

            using HttpResponseMessage response = await _http.PostAsJsonAsync($"{ServerUrl}/api/auth/refresh", new { refreshToken = _refreshToken });

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                await ClearCoreAsync();
                Changed?.Invoke();
                return false;
            }

            response.EnsureSuccessStatusCode();
            TokenResponse? tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
            if (tokens is null)
            {
                return false;
            }

            await StoreAsync(tokens);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LoadUserAsync()
    {
        try
        {
            string? token = await GetAccessTokenAsync();
            if (token is null)
            {
                return;
            }

            using HttpRequestMessage request = new(HttpMethod.Get, $"{ServerUrl}/api/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = await _http.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                User = await response.Content.ReadFromJsonAsync<UserInfoDto>(JsonOptions);
                Changed?.Invoke();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
        }
    }

    private async Task StoreAsync(TokenResponse tokens)
    {
        _accessToken = tokens.AccessToken;
        _refreshToken = tokens.RefreshToken;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn);

        await WriteSecureAsync(AccessKey, _accessToken);
        await WriteSecureAsync(RefreshKey, _refreshToken);
        await WriteSecureAsync(ExpiresKey, _expiresAt.ToUnixTimeSeconds().ToString());
    }

    private async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await ClearCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task ClearCoreAsync()
    {
        _accessToken = null;
        _refreshToken = null;
        _expiresAt = DateTimeOffset.MinValue;
        User = null;

        foreach (string key in new[] { AccessKey, RefreshKey, ExpiresKey })
        {
            try
            {
                SecureStorage.Default.Remove(key);
            }
            catch (Exception)
            {
            }
        }

        return Task.CompletedTask;
    }

    private static bool TryNormalizeServer(string input, out string normalized)
    {
        normalized = string.Empty;
        string trimmed = input.Trim().TrimEnd('/');

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        normalized = trimmed;
        return true;
    }

    private static async Task<string?> ReadSecureAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task WriteSecureAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception)
        {
        }
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
}
