using System.Net.Http.Json;
using Blazored.LocalStorage;
using B2B.Web.Models.Auth;

namespace B2B.Web.Services.Auth;

/// <summary>
/// Handles login, registration, and token persistence.
/// Uses the "public" HttpClient (no auth header) since login/register
/// are unauthenticated endpoints.
/// </summary>
public sealed class AuthService(
    IHttpClientFactory httpClientFactory,
    ILocalStorageService localStorage,
    AuthStateProvider authStateProvider) : IAuthService
{
    private const string AccessTokenKey = "b2b_access_token";
    private const string RefreshTokenKey = "b2b_refresh_token";

    private HttpClient Http => httpClientFactory.CreateClient("public");

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await Http.PostAsJsonAsync("/api/identity/auth/login", request);

            if (!response.IsSuccessStatusCode)
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                return new AuthResult(false, null, null, problem?.Detail ?? "Invalid credentials.");
            }

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (login is null)
                return new AuthResult(false, null, null, "Invalid response from server.");

            await localStorage.SetItemAsync(AccessTokenKey, login.AccessToken);
            await localStorage.SetItemAsync(RefreshTokenKey, login.RefreshToken);

            authStateProvider.NotifyUserAuthentication(login.AccessToken);

            return new AuthResult(true, login.AccessToken, login.RefreshToken, User: login);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, null, null, $"Connection error: {ex.Message}");
        }
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await Http.PostAsJsonAsync("/api/identity/auth/register", request);

            if (!response.IsSuccessStatusCode)
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                return new AuthResult(false, null, null, problem?.Detail ?? "Registration failed.");
            }

            return new AuthResult(true, null, null);
        }
        catch (Exception ex)
        {
            return new AuthResult(false, null, null, $"Connection error: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        await localStorage.RemoveItemAsync(AccessTokenKey);
        await localStorage.RemoveItemAsync(RefreshTokenKey);
        authStateProvider.NotifyUserLogout();
    }

    public async Task<string?> GetTokenAsync()
        => await localStorage.GetItemAsync<string>(AccessTokenKey);

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    // Minimal problem-details shape for error extraction.
    private sealed record ProblemDetails(string? Detail, string? Title);
}
