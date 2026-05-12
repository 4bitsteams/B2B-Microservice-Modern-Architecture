using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace B2B.Web.Services.Auth;

/// <summary>
/// Custom AuthenticationStateProvider that reads the JWT from LocalStorage
/// and parses claims directly in the browser — no server round-trip.
/// </summary>
public sealed class AuthStateProvider(ILocalStorageService localStorage)
    : AuthenticationStateProvider
{
    private const string AccessTokenKey = "b2b_access_token";

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await localStorage.GetItemAsync<string>(AccessTokenKey);

        if (string.IsNullOrWhiteSpace(token))
            return Anonymous();

        try
        {
            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            // Malformed token — treat as anonymous.
            return Anonymous();
        }
    }

    /// <summary>Called by AuthService after a successful login.</summary>
    public void NotifyUserAuthentication(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    /// <summary>Called by AuthService on logout.</summary>
    public void NotifyUserLogout()
        => NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));

    // ── JWT Parsing ───────────────────────────────────────────────────────────

    private static AuthenticationState Anonymous()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = DecodeBase64Url(payload);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes)
                   ?? new Dictionary<string, JsonElement>();

        return dict.SelectMany(kvp =>
        {
            // Arrays (e.g. "roles": ["Admin","User"]) → one Claim per value.
            if (kvp.Value.ValueKind == JsonValueKind.Array)
                return kvp.Value.EnumerateArray()
                    .Select(v => new Claim(kvp.Key, v.ToString()));

            return [new Claim(kvp.Key, kvp.Value.ToString())];
        });
    }

    private static byte[] DecodeBase64Url(string base64Url)
    {
        var padded = base64Url.Length % 4 switch
        {
            2 => base64Url + "==",
            3 => base64Url + "=",
            _ => base64Url
        };
        return Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
    }
}
