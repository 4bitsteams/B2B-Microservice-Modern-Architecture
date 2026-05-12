using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace B2B.Web.Services.Auth;

/// <summary>
/// Decorator (DelegatingHandler) that injects the JWT Bearer token into
/// every outgoing request on the "auth" HttpClient.
/// Registered as Transient — each HttpClient pipeline gets its own instance.
/// </summary>
public sealed class AuthMessageHandler(ILocalStorageService localStorage)
    : DelegatingHandler
{
    private const string AccessTokenKey = "b2b_access_token";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await localStorage.GetItemAsync<string>(AccessTokenKey);

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
