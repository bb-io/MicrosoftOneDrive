using System.Net;
using Apps.MicrosoftOneDrive.Constants;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Apps.MicrosoftOneDrive.Models.Folder.Requests;
using Apps.MicrosoftOneDrive.Models.Folder.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Actions;

[ActionList]
public class FolderActions
{
    [Action("Get folder metadata by ID", Description = "Retrieve the metadata for a folder in a drive by folder ID.")]
    public async Task<FolderMetadataDto> GetFolderMetadataById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Folder ID")] string folderId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{folderId}", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FolderWithIdNotFoundMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var folderMetadata = SerializationExtensions.DeserializeResponseContent<FolderMetadataDto>(response.Content);
        if (folderMetadata.ChildCount == null) 
            throw new Exception("Provided ID doesn't point to folder.");
        
        return folderMetadata;
    }
    
    [Action("Get folder metadata by path", Description = "Retrieve the metadata for a folder in a drive by folder path " +
                                                         "relative to the root folder.")]
    public async Task<FolderMetadataDto> GetFolderMetadataByPath(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Folder path relative to drive's root")] string folderPathRelativeToRoot)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($".//root:/{folderPathRelativeToRoot}", Method.Get, 
            authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FolderNotFoundAtPathMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var folderMetadata = SerializationExtensions.DeserializeResponseContent<FolderMetadataDto>(response.Content);
        if (folderMetadata.ChildCount == null) 
            throw new Exception($"Provided path '{folderPathRelativeToRoot}' doesn't point to folder.");
        
        return folderMetadata;
    }

    [Action("List files in folder by ID", Description = "Retrieve metadata for files contained in a folder with specified ID.")]
    public async Task<ListFilesInFolderResponse> ListFilesInFolderById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Folder ID")] string folderId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{folderId}/children", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FolderWithIdNotFoundMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var files = SerializationExtensions.DeserializeResponseContent<ListWrapper<FileMetadataDto>>(response.Content)
            .Value.Where(item => item.MimeType != null);
        return new ListFilesInFolderResponse { Files = files };
    }
    
    [Action("List files in folder by path", Description = "Retrieve metadata for files contained in a folder by folder " +
                                                          "path relative to the root folder. If path is not specified, " +
                                                          "files contained in the root are retrieved.")]
    public async Task<ListFilesInFolderResponse> ListFilesInFolderByPath(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Folder path relative to drive's root")] string? folderPathRelativeToRoot)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        MicrosoftOneDriveRequest request;
        if (folderPathRelativeToRoot != null) 
            request = new MicrosoftOneDriveRequest($".//root:/{folderPathRelativeToRoot}:/children", Method.Get, 
                authenticationCredentialsProviders);
        else 
            request = new MicrosoftOneDriveRequest("/root/children", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FolderNotFoundAtPathMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var files = SerializationExtensions.DeserializeResponseContent<ListWrapper<FileMetadataDto>>(response.Content)
            .Value.Where(item => item.MimeType != null);
        return new ListFilesInFolderResponse { Files = files };
    }
    
    [Action("Create folder in parent folder with ID", Description = "Create a new folder in parent folder with specified ID.")]
    public async Task<FolderMetadataDto> CreateFolderInParentFolderWithId(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] CreateFolderInParentFolderWithIdRequest input)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{input.ParentFolderId}/children", Method.Post, 
            authenticationCredentialsProviders);
        request.AddJsonBody(new
        {
            Name = input.FolderName,
            Folder = new { }
        });
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound
            || response.StatusCode == HttpStatusCode.InternalServerError)
            throw new Exception(ErrorMessages.FolderWithIdNotFoundMessage + " or " + ErrorMessages.InvalidFolderName.ToLower());
        
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new Exception(ErrorMessages.FolderWithNameAlreadyExists);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);

        var folderMetadata = SerializationExtensions.DeserializeResponseContent<FolderMetadataDto>(response.Content);
        return folderMetadata;
    }
    
    [Action("Create folder at path", Description = "Create a folder at the path relative to the root folder. If path " +
                                                   "is not specified, folder is created at the root.")]
    public async Task<FolderMetadataDto> CreateFolderAtPath(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] CreateFolderAtPathRequest input)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        MicrosoftOneDriveRequest request;
        if (input.PathRelativeToRoot != null) 
            request = new MicrosoftOneDriveRequest($".//root:/{input.PathRelativeToRoot}:/children", Method.Post, 
                authenticationCredentialsProviders);
        else
            request = new MicrosoftOneDriveRequest("/root/children", Method.Post, authenticationCredentialsProviders);

        request.AddJsonBody(new
        {
            Name = input.FolderName,
            Folder = new { }
        });
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FolderNotFoundAtPathMessage + " or " + ErrorMessages.InvalidFolderName.ToLower());
        
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new Exception(ErrorMessages.FolderWithNameAlreadyExists);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);

        var folderMetadata = SerializationExtensions.DeserializeResponseContent<FolderMetadataDto>(response.Content);
        return folderMetadata;
    }
}