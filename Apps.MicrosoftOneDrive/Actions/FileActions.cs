using System.Net;
using Apps.MicrosoftOneDrive.Constants;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Apps.MicrosoftOneDrive.Models.File.Requests;
using Apps.MicrosoftOneDrive.Models.File.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Actions;

[ActionList]
public class FileActions
{
    [Action("Get file metadata by ID", Description = "Retrieve the metadata for a file in a drive by file ID.")]
    public async Task<FileMetadataDto> GetFileMetadataById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] string fileId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{fileId}", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileWithIdNotFoundMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var fileMetadata = SerializationExtensions.DeserializeResponseContent<FileMetadataDto>(response.Content);
        if (fileMetadata.MimeType == null) 
            throw new Exception("Provided ID points to folder, not file.");
        
        return fileMetadata;
    }
    
    [Action("Get file metadata by file path", Description = "Retrieve the metadata for a file in a drive by file path " +
                                                            "relative to the root folder.")]
    public async Task<FileMetadataDto> GetFileMetadataByFilePath(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File path relative to drive's root")] string filePathRelativeToRoot)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($".//root:/{filePathRelativeToRoot}", Method.Get, 
            authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileNotFoundAtFilePathMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
        
        var fileMetadata = SerializationExtensions.DeserializeResponseContent<FileMetadataDto>(response.Content);
        if (fileMetadata.MimeType == null)
            throw new Exception($"Provided path '{filePathRelativeToRoot}' points to folder, not file.");
        
        return fileMetadata;
    }
    
    [Action("Download file by ID", Description = "Download a file in a drive by file ID.")]
    public async Task<DownloadFileResponse> DownloadFileById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] string fileId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{fileId}/content", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileWithIdNotFoundMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
        
        var fileBytes = response.RawBytes;
        var filenameHeader = response.ContentHeaders.First(h => h.Name == "Content-Disposition");
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
        var request = new MicrosoftOneDriveRequest($".//root:/{filePathRelativeToRoot}:/content", Method.Get, 
            authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileNotFoundAtFilePathMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
        
        var fileBytes = response.RawBytes;
        var filenameHeader = response.ContentHeaders.First(h => h.Name == "Content-Disposition");
        var filename = filenameHeader.Value.ToString().Split('"')[1];
        return new DownloadFileResponse
        {
            Filename = filename,
            File = fileBytes
        };
    }
    
    [Action("Upload file", Description = "Upload file to parent folder with specified ID. File must be up to 4MB in size.")]
    public async Task<FileMetadataDto> UploadFile(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] UploadFileRequest input)
    {
        const int fourMegabytesInBytes = 4194304;
        if (input.File.Length > fourMegabytesInBytes)
            throw new ArgumentException("Size of the file must be under 4 MB.");
        
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($".//items/{input.ParentFolderId}:/{input.Filename}:/content", 
            Method.Put, authenticationCredentialsProviders);
        if (!MimeTypes.TryGetMimeType(input.Filename, out var mimeType))
            mimeType = "application/octet-stream";
        request.AddParameter(mimeType, input.File, ParameterType.RequestBody);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FolderWithIdNotFoundMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var fileMetadata = SerializationExtensions.DeserializeResponseContent<FileMetadataDto>(response.Content);
        return fileMetadata;
    }
    
    [Action("Delete file", Description = "Delete file in a drive by file ID.")]
    public async Task DeleteFile(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] string fileId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{fileId}", Method.Delete, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileWithIdNotFoundMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
    }
}