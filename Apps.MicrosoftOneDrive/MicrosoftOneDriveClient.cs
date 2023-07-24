using Blackbird.Applications.Sdk.Common.Authentication;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Apps.MicrosoftOneDrive;

public class MicrosoftOneDriveClient : GraphServiceClient
{
    public MicrosoftOneDriveClient(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders) :
        base(GetAuthenticationProvider(authenticationCredentialsProviders))
    {
    }
    
    private static BaseBearerTokenAuthenticationProvider GetAuthenticationProvider(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
    {
        var token = authenticationCredentialsProviders.First(p => p.KeyName == "Authorization").Value;
        var accessTokenProvider = new AccessTokenProvider(token);
        return new BaseBearerTokenAuthenticationProvider(accessTokenProvider);
    }
}