using System.Net;
using Apps.MicrosoftOneDrive.Constants;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Apps.MicrosoftOneDrive.Models.Requests;
using Apps.MicrosoftOneDrive.Models.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using RestSharp;

namespace Apps.MicrosoftOneDrive.Actions;

[ActionList]
public class StorageActions
{
    #region File actions
    
    [Action("Get file metadata by ID", Description = "Retrieve the metadata for a file in a drive by file ID.")]
    public async Task<FileMetadataDto> GetFileMetadataById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] string fileId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{fileId}", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileOrFolderWithIdNotFoundMessage);
        
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
            throw new Exception(ErrorMessages.FileOrFolderNotFoundAtPathMessage);
        
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
            throw new Exception(ErrorMessages.FileOrFolderWithIdNotFoundMessage);
        
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
            throw new Exception(ErrorMessages.FileOrFolderNotFoundAtPathMessage);
        
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
    
    [Action("Upload file in folder by ID", Description = "Upload file to parent folder with specified ID. File must " +
                                                         "be up to 4MB in size.")]
    public async Task<FileMetadataDto> UploadFileInFolderById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Parent folder ID")] string parentFolderId,
        [ActionParameter] UploadFileRequest input)
    {
        const int fourMegabytesInBytes = 4194304;
        if (input.File.Length > fourMegabytesInBytes)
            throw new ArgumentException("Size of the file must be under 4 MB.");
        
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($".//items/{parentFolderId}:/{input.Filename}:/content", 
            Method.Put, authenticationCredentialsProviders);
        if (!MimeTypes.TryGetMimeType(input.Filename, out var mimeType))
            mimeType = "application/octet-stream";
        request.AddParameter(mimeType, input.File, ParameterType.RequestBody);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileOrFolderWithIdNotFoundMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var fileMetadata = SerializationExtensions.DeserializeResponseContent<FileMetadataDto>(response.Content);
        return fileMetadata;
    }
    
    [Action("Upload file at path", Description = "Upload file to parent folder specified as path relative to the root " +
                                                 "folder. File must be up to 4MB in size. If path is not specified, " +
                                                 "file is uploaded to the root folder. If the specified path does not " +
                                                 "exist, the corresponding folder structure is created.")]
    public async Task<FileMetadataDto> UploadFileAtPath(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Path relative to drive's root")] string? pathRelativeToRoot,
        [ActionParameter] UploadFileRequest input)
    {
        const int fourMegabytesInBytes = 4194304;
        if (input.File.Length > fourMegabytesInBytes)
            throw new ArgumentException("Size of the file must be under 4 MB.");
        
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        MicrosoftOneDriveRequest request;
        if (pathRelativeToRoot != null) 
            request = new MicrosoftOneDriveRequest($".//root:/{pathRelativeToRoot}/{input.Filename}:/content", Method.Put, 
                authenticationCredentialsProviders);
        else
            request = new MicrosoftOneDriveRequest($".//root:/{input.Filename}:/content", Method.Put, 
                authenticationCredentialsProviders);

        if (!MimeTypes.TryGetMimeType(input.Filename, out var mimeType))
            mimeType = "application/octet-stream";
        request.AddParameter(mimeType, input.File, ParameterType.RequestBody);
        var response = await client.ExecuteAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var fileMetadata = SerializationExtensions.DeserializeResponseContent<FileMetadataDto>(response.Content);
        return fileMetadata;
    }
    
    #endregion
    
    #region Folder actions
    
    [Action("Get folder metadata by ID", Description = "Retrieve the metadata for a folder in a drive by folder ID.")]
    public async Task<FolderMetadataDto> GetFolderMetadataById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Folder ID")] string folderId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{folderId}", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileOrFolderWithIdNotFoundMessage);
        
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
            throw new Exception(ErrorMessages.FileOrFolderNotFoundAtPathMessage);
        
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
            throw new Exception(ErrorMessages.FileOrFolderWithIdNotFoundMessage);
        
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
            throw new Exception(ErrorMessages.FileOrFolderNotFoundAtPathMessage);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
            
        var files = SerializationExtensions.DeserializeResponseContent<ListWrapper<FileMetadataDto>>(response.Content)
            .Value.Where(item => item.MimeType != null);
        return new ListFilesInFolderResponse { Files = files };
    }
    
    [Action("Create folder in parent folder with ID", Description = "Create a new folder in parent folder with specified ID.")]
    public async Task<FolderMetadataDto> CreateFolderInParentFolderWithId(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Parent folder ID")] string parentFolderId,
        [ActionParameter] [Display("Folder name")] string folderName)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{parentFolderId}/children", Method.Post, 
            authenticationCredentialsProviders);
        request.AddJsonBody(new
        {
            Name = folderName,
            Folder = new { }
        });
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound
            || response.StatusCode == HttpStatusCode.InternalServerError)
            throw new Exception(ErrorMessages.FileOrFolderWithIdNotFoundMessage + " or " + ErrorMessages.InvalidFolderName.ToLower());
        
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
        [ActionParameter] [Display("Path relative to drive's root")] string? pathRelativeToRoot,
        [ActionParameter] [Display("Folder name")] string folderName)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        MicrosoftOneDriveRequest request;
        if (pathRelativeToRoot != null) 
            request = new MicrosoftOneDriveRequest($".//root:/{pathRelativeToRoot}:/children", Method.Post, 
                authenticationCredentialsProviders);
        else
            request = new MicrosoftOneDriveRequest("/root/children", Method.Post, authenticationCredentialsProviders);

        request.AddJsonBody(new
        {
            Name = folderName,
            Folder = new { }
        });
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileOrFolderNotFoundAtPathMessage + " or " + ErrorMessages.InvalidFolderName.ToLower());
        
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new Exception(ErrorMessages.FolderWithNameAlreadyExists);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);

        var folderMetadata = SerializationExtensions.DeserializeResponseContent<FolderMetadataDto>(response.Content);
        return folderMetadata;
    }
    
    #endregion
    
    [Action("Delete file or folder by ID", Description = "Delete file or folder in a drive by ID.")]
    public async Task DeleteFileOrFolderById(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File or folder ID")] string fileOrFolderId)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($"/items/{fileOrFolderId}", Method.Delete, authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileOrFolderWithIdNotFoundMessage);
        
        if (response.StatusCode == HttpStatusCode.Forbidden) 
            throw new Exception(ErrorMessages.DeletingRootFolderIsForbidden);
        
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
    }
    
    [Action("Delete file or folder at path", Description = "Delete file or folder at the path relative to the root folder.")]
    public async Task DeleteFileOrFolderAtPath(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Item's path relative to drive's root")] string itemPathRelativeToRoot)
    {
        var client = new MicrosoftOneDriveClient(authenticationCredentialsProviders);
        var request = new MicrosoftOneDriveRequest($".//root:/{itemPathRelativeToRoot}", Method.Delete, 
            authenticationCredentialsProviders);
        var response = await client.ExecuteAsync(request);
        
        if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound)
            throw new Exception(ErrorMessages.FileOrFolderNotFoundAtPathMessage);
        
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new Exception(ErrorMessages.DeletingRootFolderIsForbidden);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new Exception(ErrorMessages.UnauthorizedMessage);
    }
}