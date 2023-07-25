using System.Net;
using Apps.MicrosoftOneDrive.Constants;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Models.File.Requests;
using Apps.MicrosoftOneDrive.Models.File.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Newtonsoft.Json;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Actions;

[ActionList]
public class FileActions
{
    [Action("Get file metadata by ID", Description = "Retrieve the metadata for a file in a drive by file ID.")]
    public async Task<FileMetadataResponse> GetFileMetadataById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] string fileId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var oneDriveRequest = new MicrosoftOneDriveRequest($"/items/{fileId}", Method.Get, 
            authenticationCredentialsProviders);
        var restResponse = await client.ExecuteAsync(oneDriveRequest);
        if (restResponse.StatusCode == HttpStatusCode.BadRequest || restResponse.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileWithIdNotFoundMessage);
        if (restResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var fileMetadata = DeserializeResponseContent<FileMetadataDto>(restResponse.Content);
        if (fileMetadata.File == null) 
            throw new Exception("Provided ID points to folder, not file.");
        return new FileMetadataResponse(fileMetadata);
    }
    
    [Action("Get file metadata by file path", Description = "Retrieve the metadata for a file in a drive by file path " +
                                                            "relative to the root folder.")]
    public async Task<FileMetadataResponse> GetFileMetadataByFilePath(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File path relative to drive's root")] string filePathRelativeToRoot)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var oneDriveRequest = new MicrosoftOneDriveRequest($".//root:/{filePathRelativeToRoot}", Method.Get, 
            authenticationCredentialsProviders);
        var restResponse = await client.ExecuteAsync(oneDriveRequest);
        if (restResponse.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileNotFoundAtFilePathMessage);
        if (restResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
        
        var fileMetadata = DeserializeResponseContent<FileMetadataDto>(restResponse.Content);
        if (fileMetadata.File == null)
            throw new Exception($"Provided path '{filePathRelativeToRoot}' points to folder, not file.");
        return new FileMetadataResponse(fileMetadata);
    }
    
    [Action("Download file by ID", Description = "Download a file in a drive by file ID.")]
    public async Task<DownloadFileResponse> DownloadFileById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] string fileId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var oneDriveRequest = new MicrosoftOneDriveRequest($"/items/{fileId}/content", Method.Get, 
            authenticationCredentialsProviders);
        var restResponse = await client.ExecuteAsync(oneDriveRequest);
        if (restResponse.StatusCode == HttpStatusCode.BadRequest || restResponse.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileWithIdNotFoundMessage);
        if (restResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
        
        var fileBytes = restResponse.RawBytes;
        var filenameHeader = restResponse.ContentHeaders.First(h => h.Name == "Content-Disposition");
        var filename = filenameHeader.Value.ToString().Split('"')[1];
        return new DownloadFileResponse
        {
            Filename = filename,
            File = fileBytes
        };
    }
    
    [Action("Download file by file path", Description = "Download a file in a drive by file path relative to the root folder.")]
    public async Task<DownloadFileResponse> DownloadFileByFilePath(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File path relative to drive's root")] string filePathRelativeToRoot)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var oneDriveRequest = new MicrosoftOneDriveRequest($".//root:/{filePathRelativeToRoot}:/content", Method.Get, 
            authenticationCredentialsProviders);
        var restResponse = await client.ExecuteAsync(oneDriveRequest);
        if (restResponse.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileNotFoundAtFilePathMessage);
        if (restResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
        
        var fileBytes = restResponse.RawBytes;
        var filenameHeader = restResponse.ContentHeaders.First(h => h.Name == "Content-Disposition");
        var filename = filenameHeader.Value.ToString().Split('"')[1];
        return new DownloadFileResponse
        {
            Filename = filename,
            File = fileBytes
        };
    }
    
    [Action("Upload file", Description = "Upload file to parent folder with specified ID. File must be up to 4MB in size.")]
    public async Task<FileMetadataResponse> UploadFile(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] UploadFileRequest request)
    {
        const int fourMegabytesInBytes = 4194304;
        if (request.File.Length > fourMegabytesInBytes)
            throw new ArgumentException("Size of the file must be under 4 MB.");
        
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var oneDriveRequest = new MicrosoftOneDriveRequest($".//items/{request.ParentFolderId}:/{request.Filename}:/content", 
            Method.Put, authenticationCredentialsProviders);
        if (!MimeTypes.TryGetMimeType(request.Filename, out var mimeType))
            mimeType = "application/octet-stream";
        
        oneDriveRequest.AddParameter(mimeType, request.File, ParameterType.RequestBody); 
        var restResponse = await client.ExecuteAsync(oneDriveRequest);
        if (restResponse.StatusCode == HttpStatusCode.BadRequest || restResponse.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FolderWithIdNotFoundMessage);
        if (restResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var fileMetadata = DeserializeResponseContent<FileMetadataDto>(restResponse.Content);
        return new FileMetadataResponse(fileMetadata);
    }
    
    [Action("Delete file", Description = "Delete file in a drive by file ID.")]
    public async Task DeleteFile(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] string fileId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var oneDriveRequest = new MicrosoftOneDriveRequest($"/items/{fileId}", Method.Delete, 
            authenticationCredentialsProviders);
        var restResponse = await client.ExecuteAsync(oneDriveRequest);
        if (restResponse.StatusCode == HttpStatusCode.BadRequest || restResponse.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileWithIdNotFoundMessage);
        if (restResponse.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
    }

    private T DeserializeResponseContent<T>(string content)
    {
        var deserializedContent = JsonConvert.DeserializeObject<T>(content, new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore
            }
        );
        return deserializedContent;
    }
}