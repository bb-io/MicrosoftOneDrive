using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using RestSharp;

namespace Apps.MicrosoftOneDrive;

public class MicrosoftOneDriveClient : RestClient
{
    public MicrosoftOneDriveClient() 
        : base(new RestClientOptions
        {
            ThrowOnAnyError = false, BaseUrl = GetBaseUrl() 
        }) { }

    private static Uri GetBaseUrl()
    {
        return new Uri("https://graph.microsoft.com/v1.0/me/drive");
    }
    
    public async Task<T> ExecuteWithHandling<T>(RestRequest request)
    {
        var response = await ExecuteWithHandling(request);
        return SerializationExtensions.DeserializeResponseContent<T>(response.Content);
    }
    
    public async Task<RestResponse> ExecuteWithHandling(RestRequest request)
    {
        var response = await ExecuteAsync(request);
        
        if (response.IsSuccessful)
            return response;

        throw ConfigureErrorException(response.Content);
    }

    private Exception ConfigureErrorException(string responseContent)
    {
        var error = SerializationExtensions.DeserializeResponseContent<ErrorDto>(responseContent);
        return new($"{error.Error.Code}: {error.Error.Message}");
    }
}