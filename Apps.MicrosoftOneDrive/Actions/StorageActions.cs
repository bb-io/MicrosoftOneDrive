using System.Net.Mime;
using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Apps.MicrosoftOneDrive.Models.Requests;
using Apps.MicrosoftOneDrive.Models.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using RestSharp;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Invocation;

namespace Apps.MicrosoftOneDrive.Actions;

[ActionList]
public class StorageActions
{
    private readonly IFileManagementClient _fileManagementClient;

    public StorageActions(IFileManagementClient fileManagementClient)
    {
        _fileManagementClient = fileManagementClient;
    }
    
    #region File actions

    [Action("Get file metadata", Description = "Retrieve the metadata for a file in a drive.")]
    public async Task<FileMetadataDto> GetFileMetadataById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] [DataSource(typeof(FileDataSourceHandler))] string fileId)
    {
        var client = new MicrosoftOneDriveClient();
        var request = new MicrosoftOneDriveRequest($"/items/{fileId}", Method.Get, authenticationCredentialsProviders);
        var fileMetadata = await client.ExecuteWithHandling<FileMetadataDto>(request);
        return fileMetadata;
    }

    [Action("List changed files", Description = "List all files that have been created or modified during past hours. " +
                                                "If number of hours is not specified, files changed during past 24 " +
                                                "hours are listed.")]
    public async Task<ListFilesResponse> ListChangedFiles(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Hours")] int? hours)
    {
        var client = new MicrosoftOneDriveClient();
        var endpoint = "/root/search(q='.')?$orderby=lastModifiedDateTime desc";
        var startDateTime = (DateTime.Now - TimeSpan.FromHours(hours ?? 24)).ToUniversalTime();
        var changedFiles = new List<FileMetadataDto>();
        int filesCount;

        do
        {
            var request = new MicrosoftOneDriveRequest(endpoint, Method.Get, authenticationCredentialsProviders);
            var result = await client.ExecuteWithHandling<ListWrapper<FileMetadataDto>>(request);
            var files = result.Value.Where(item => item.MimeType != null && item.LastModifiedDateTime >= startDateTime);
            filesCount = files.Count();
            changedFiles.AddRange(files);
            endpoint = result.ODataNextLink?.Split("drive")[1];
        } while (endpoint != null && filesCount != 0);

        return new ListFilesResponse { Files = changedFiles };
    }

    [Action("Download file", Description = "Download a file in a drive.")]
    public async Task<DownloadFileResponse> DownloadFileById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] [DataSource(typeof(FileDataSourceHandler))] string fileId)
    {
        var client = new MicrosoftOneDriveClient();
        var request = new MicrosoftOneDriveRequest($"/items/{fileId}/content", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteWithHandling(request);
        
        var filenameHeader = response.ContentHeaders.First(h => h.Name == "Content-Disposition");
        var filename = filenameHeader.Value.ToString().Split('"')[1];
        var contentType = response.ContentType == MediaTypeNames.Text.Plain
            ? MediaTypeNames.Text.RichText
            : response.ContentType;

        FileReference file;
        using(var stream = new MemoryStream(response.RawBytes))
        {
            file = await _fileManagementClient.UploadAsync(stream, contentType, filename);
        }
        
        return new DownloadFileResponse { File = file };
    }

    [Action("Upload file to folder", Description = "Upload a file to a parent folder.")]
    public async Task<FileMetadataDto> UploadFileInFolderById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Parent folder ID")] [DataSource(typeof(FolderDataSourceHandler))] string parentFolderId,
        [ActionParameter] UploadFileRequest input)
    {
        const int fourMegabytesInBytes = 4194304;
        var client = new MicrosoftOneDriveClient();

        var file = await _fileManagementClient.DownloadAsync(input.File);
    
        var fileStream = new MemoryStream();
        await file.CopyToAsync(fileStream);
        fileStream.Position = 0;

        var fileSize = fileStream.Length;
        var contentType = Path.GetExtension(input.File.Name) == ".txt"
            ? MediaTypeNames.Text.Plain
            : input.File.ContentType;
        var fileMetadata = new FileMetadataDto();

        if (fileSize < fourMegabytesInBytes)
        {
            var uploadRequest = new MicrosoftOneDriveRequest($".//items/{parentFolderId}:/{input.File.Name}:/content" +
                                                             $"?@microsoft.graph.conflictBehavior={input.ConflictBehavior}",
                Method.Put, authenticationCredentialsProviders);

            uploadRequest.AddParameter("application/octet-stream", await fileStream.GetByteData(), ParameterType.RequestBody);
            fileMetadata = await client.ExecuteWithHandling<FileMetadataDto>(uploadRequest);
        }
        else
        {
            const int chunkSize = 3932160;

            var createUploadSessionRequest = new MicrosoftOneDriveRequest(
                $".//items/{parentFolderId}:/{input.File.Name}:/createUploadSession", Method.Post,
                authenticationCredentialsProviders);
            createUploadSessionRequest.AddJsonBody($@"
                {{
                    ""deferCommit"": false,
                    ""item"": {{
                        ""@microsoft.graph.conflictBehavior"": ""{input.ConflictBehavior}"",
                        ""name"": ""{input.File.Name}""
                    }}
                }}");

            var resumableUploadResult = await client.ExecuteWithHandling<ResumableUploadDto>(createUploadSessionRequest);
            var uploadUrl = new Uri(resumableUploadResult.UploadUrl);
            var baseUrl = uploadUrl.GetLeftPart(UriPartial.Authority);
            var endpoint = uploadUrl.PathAndQuery;
            var uploadClient = new RestClient(new RestClientOptions { BaseUrl = new(baseUrl) });

            var fileBytes = await fileStream.GetByteData();
            do
            {
                var startByte = int.Parse(resumableUploadResult.NextExpectedRanges.First().Split("-")[0]);
                var buffer = fileBytes.Skip(startByte).Take(chunkSize).ToArray();
                var bufferSize = buffer.Length;
                
                var uploadRequest = new RestRequest(endpoint, Method.Put);
                uploadRequest.AddParameter(contentType, buffer, ParameterType.RequestBody);
                uploadRequest.AddHeader("Content-Length", bufferSize);
                uploadRequest.AddHeader("Content-Range", 
                    $"bytes {startByte}-{startByte + bufferSize - 1}/{fileSize}");
                
                var uploadResponse = await uploadClient.ExecuteAsync(uploadRequest);
                var responseContent = uploadResponse.Content;

                if (!uploadResponse.IsSuccessful)
                {
                    var error = SerializationExtensions.DeserializeResponseContent<ErrorDto>(responseContent);
                    throw new PluginApplicationException(error.Error.Message);
                }
                
                resumableUploadResult =
                    SerializationExtensions.DeserializeResponseContent<ResumableUploadDto>(responseContent);

                if (resumableUploadResult.NextExpectedRanges == null)
                    fileMetadata = SerializationExtensions.DeserializeResponseContent<FileMetadataDto>(responseContent);
                
            } while (resumableUploadResult.NextExpectedRanges != null);
        }

        return fileMetadata;
    }
    
    [Action("Delete file", Description = "Delete file in a drive.")]
    public async Task DeleteFileId(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File ID")] [DataSource(typeof(FileDataSourceHandler))] string fileId)
    {
        var client = new MicrosoftOneDriveClient();
        var request = new MicrosoftOneDriveRequest($"/items/{fileId}", Method.Delete, authenticationCredentialsProviders); 
        await client.ExecuteWithHandling(request);
    }
    
    #endregion
    
    #region Folder actions
    
    [Action("Get folder metadata", Description = "Retrieve the metadata for a folder in a drive.")]
    public async Task<FolderMetadataDto> GetFolderMetadataById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Folder")] [DataSource(typeof(FolderDataSourceHandler))] string folderId)
    {
        var client = new MicrosoftOneDriveClient();
        var request = new MicrosoftOneDriveRequest($"/items/{folderId}", Method.Get, authenticationCredentialsProviders);
        var folderMetadata = await client.ExecuteWithHandling<FolderMetadataDto>(request);
        return folderMetadata;
    }

    [Action("List files in folder", Description = "Retrieve metadata for files contained in a folder.")]
    public async Task<ListFilesResponse> ListFilesInFolderById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Folder")] [DataSource(typeof(FolderDataSourceHandler))] string folderId)
    {
        var client = new MicrosoftOneDriveClient();
        var filesInFolder = new List<FileMetadataDto>();
        var endpoint = $"/items/{folderId}/children";
        
        do
        {
            var request = new MicrosoftOneDriveRequest(endpoint, Method.Get, authenticationCredentialsProviders);
            var result = await client.ExecuteWithHandling<ListWrapper<FileMetadataDto>>(request);
            var files = result.Value.Where(item => item.MimeType != null);
            filesInFolder.AddRange(files);
            endpoint = result.ODataNextLink?.Split("drive")[1];
        } while (endpoint != null);
        
        return new ListFilesResponse { Files = filesInFolder };
    }
    
    [Action("Create folder in parent folder", Description = "Create a new folder in parent folder.")]
    public async Task<FolderMetadataDto> CreateFolderInParentFolderWithId(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Parent folder")] [DataSource(typeof(FolderDataSourceHandler))] string parentFolderId,
        [ActionParameter] [Display("Folder name")] string folderName)
    {
        var client = new MicrosoftOneDriveClient();
        var request = new MicrosoftOneDriveRequest($"/items/{parentFolderId}/children", Method.Post, 
            authenticationCredentialsProviders);
        request.AddJsonBody(new
        {
            Name = folderName,
            Folder = new { }
        });

        var folderMetadata = await client.ExecuteWithHandling<FolderMetadataDto>(request);
        return folderMetadata;
    }
    
    [Action("Delete folder", Description = "Delete folder in a drive.")]
    public async Task DeleteFolderById(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Folder")] [DataSource(typeof(FolderDataSourceHandler))] string folderId)
    {
        var client = new MicrosoftOneDriveClient();
        var request = new MicrosoftOneDriveRequest($"/items/{folderId}", Method.Delete, authenticationCredentialsProviders); 
        await client.ExecuteWithHandling(request);
    }

    [Action("Search folders", Description = "")]

    public async Task<IEnumerable<SimpleFolderDto>> SearhFolders(IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter][Display("Folder name")]string folderName)
    {
        var client = new MicrosoftOneDriveClient();
        var endpoint = "/list/items?$select=id&$expand=driveItem($select=id,name,parentReference,folder)";

        var request = new MicrosoftOneDriveRequest(endpoint, Method.Get, authenticationCredentialsProviders);
        request.AddHeader("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
        var folders = await client.ExecuteWithHandling<ListWrapper<DriveItemWrapper<SimpleFolderDto>>>(request);
        var filteredFolders = folders.Value
                .Select(w => w.DriveItem)
                .Where(i => i.Name.Contains(folderName, StringComparison.OrdinalIgnoreCase));
        return filteredFolders;
    }
        
    #endregion

    [Action("DEBUG: Get auth data", Description = "Can be used only for debugging purposes.")]
    public List<AuthenticationCredentialsProvider> GetAuthenticationCredentialsProviders(
    IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders)
    {
        return authenticationCredentialsProviders.ToList();
    }
}