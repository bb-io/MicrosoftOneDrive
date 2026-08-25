using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Authentication.OAuth2;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;
using System.Globalization;
using System.Text.Json;

namespace Apps.MicrosoftOneDrive.Auth.OAuth2;

public class OAuth2TokenService : BaseInvocable, IOAuth2TokenService, ITokenRefreshable
{
    private const string TokenUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    private const string ExpiresAtKeyName = "expires_at";
    private const string RefreshTokenKeyName = "refresh_token";
    private const string ExpiresInKeyName = "expires_in";
    private const string LogPrefix = "[MicrosoftOneDrive][OAuth]";

    public OAuth2TokenService(InvocationContext invocationContext) : base(invocationContext)
    {
    }

    public bool IsRefreshToken(Dictionary<string, string> values)
        => TryGetExpiresAtUtc(values, out var expiresAt) && DateTime.UtcNow > expiresAt;

    public int? GetRefreshTokenExprireInMinutes(Dictionary<string, string> values)
    {
        if (!TryGetExpiresAtUtc(values, out var expireDate))
            return null;

        var difference = expireDate - DateTime.UtcNow;

        return (int)difference.TotalMinutes - 5;
    }

    public async Task<Dictionary<string, string>> RefreshToken(Dictionary<string, string> values, 
        CancellationToken cancellationToken) 
    { 
        const string grantType = "refresh_token";
        if (!values.TryGetValue(RefreshTokenKeyName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new PluginMisconfigurationException(
                "The Microsoft OneDrive connection has no refresh token stored, so it cannot be renewed automatically. " +
                "Please reconnect your Microsoft OneDrive connection.");
        }

        InvocationContext.Logger?.LogError(
            $"{LogPrefix} Starting refresh token flow. RefreshToken: {GetTokenPreview(refreshToken)}; LocalExpiresAt: {GetSafeValue(values, ExpiresAtKeyName)}; MinutesUntilLocalExpiry: {GetRefreshTokenExprireInMinutes(values)?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}",
            null);

        var bodyParameters = new Dictionary<string, string>
        {
            { "grant_type", grantType },
            { RefreshTokenKeyName, refreshToken },
            { "client_id", ApplicationConstants.ClientId },
            { "client_secret", ApplicationConstants.ClientSecret }
        };
        return await RequestToken(bodyParameters, cancellationToken);
    }
    
    public async Task<Dictionary<string, string?>> RequestToken(string state, string code, 
        Dictionary<string, string> values, CancellationToken cancellationToken)
    { 
        const string grantType = "authorization_code"; 
        var bodyParameters = new Dictionary<string, string> 
        { 
            { "code", code },
            { "grant_type", grantType },
            { "client_id", ApplicationConstants.ClientId }, 
            { "client_secret", ApplicationConstants.ClientSecret },
            { "redirect_uri", $"{InvocationContext.UriInfo.BridgeServiceUrl.ToString().TrimEnd('/')}/AuthorizationCode" }
        };
        return await RequestToken(bodyParameters, cancellationToken);
    }

    public Task RevokeToken(Dictionary<string, string> values)
    { 
        throw new NotImplementedException();
    }
    
    private async Task<Dictionary<string, string>> RequestToken(Dictionary<string, string> bodyParameters, 
        CancellationToken cancellationToken)
    { 
        var grantType = GetSafeValue(bodyParameters, "grant_type");
        var utcNow = DateTime.UtcNow;
        using HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        using var httpContent = new FormUrlEncodedContent(bodyParameters);
        using var response = await httpClient.PostAsync(TokenUrl, httpContent, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            InvocationContext.Logger?.LogError(
                $"{LogPrefix} OAuth token request failed. GrantType: {grantType}; StatusCode: {(int)response.StatusCode} {response.StatusCode}; Body: {responseContent}",
                null);

            throw new PluginApplicationException(
                $"Microsoft OAuth token request failed ({(int)response.StatusCode} {response.StatusCode}). {responseContent}");
        }

        var resultDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent)?
                                       .ToDictionary(r => r.Key, r => r.Value?.ToString())
                                   ?? throw new InvalidOperationException($"Invalid response content: {responseContent}");

        if (!resultDictionary.TryGetValue(ExpiresInKeyName, out var expiresInValue) ||
            !int.TryParse(expiresInValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresIn))
        {
            InvocationContext.Logger?.LogError(
                $"{LogPrefix} OAuth token response has no usable '{ExpiresInKeyName}'. GrantType: {grantType}; Body: {responseContent}",
                null);

            throw new PluginApplicationException(
                $"Microsoft OAuth token response did not contain a valid '{ExpiresInKeyName}' value. {responseContent}");
        }

        // Microsoft does not always return a new refresh token; keep the one we already have so the connection stays renewable.
        resultDictionary.TryGetValue(RefreshTokenKeyName, out var returnedRefreshToken);
        var refreshTokenReturned = !string.IsNullOrWhiteSpace(returnedRefreshToken);
        if (!refreshTokenReturned && bodyParameters.TryGetValue(RefreshTokenKeyName, out var previousRefreshToken))
            resultDictionary[RefreshTokenKeyName] = previousRefreshToken;

        var expiresAt = utcNow.AddSeconds(expiresIn);
        resultDictionary[ExpiresAtKeyName] = expiresAt.ToString("O");

        resultDictionary.TryGetValue(RefreshTokenKeyName, out var nextRefreshToken);
        InvocationContext.Logger?.LogError(
            $"{LogPrefix} OAuth token request succeeded. GrantType: {grantType}; ExpiresInSeconds: {expiresIn}; LocalExpiresAt: {expiresAt:O}; RefreshTokenReturned: {refreshTokenReturned}; NextRefreshToken: {GetTokenPreview(nextRefreshToken)}",
            null);

        return resultDictionary;
    }

    private static bool TryGetExpiresAtUtc(Dictionary<string, string> values, out DateTime expiresAtUtc)
    {
        expiresAtUtc = default;

        if (!values.TryGetValue(ExpiresAtKeyName, out var expireValue) || string.IsNullOrWhiteSpace(expireValue))
            return false;

        // Values written before this app used the round-trip ("O") format carry no offset, but were always UTC.
        // They were also formatted with the current culture, hence the second attempt.
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        return DateTime.TryParse(expireValue, CultureInfo.InvariantCulture, styles, out expiresAtUtc)
               || DateTime.TryParse(expireValue, CultureInfo.CurrentCulture, styles, out expiresAtUtc);
    }

    private static string GetSafeValue(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : "n/a";

    private static string GetTokenPreview(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "missing";

        return token.Length <= 8 ? token : token[..8];
    }
}