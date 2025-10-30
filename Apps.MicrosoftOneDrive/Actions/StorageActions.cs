using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Apps.MicrosoftOneDrive.Invocables;
using Apps.MicrosoftOneDrive.Models.Requests;
using Apps.MicrosoftOneDrive.Models.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Exceptions;
using Blackbird.Applications.Sdk.Common.Files;
using Blackbird.Applications.Sdk.Common.Invocation;
using Blackbird.Applications.Sdk.Utils.Extensions.Files;
using Blackbird.Applications.SDK.Blueprints;
using Blackbird.Applications.SDK.Extensions.FileManagement.Interfaces;
using Blackbird.Applications.SDK.Extensions.FileManagement.Models.FileDataSourceItems;
using RestSharp;
using System.Net.Mime;

namespace Apps.MicrosoftOneDrive.Actions;

[ActionList("Files")]
public class StorageActions(InvocationContext context, IFileManagementClient _fileManagementClient) : OneDriveInvocable(context)
{

    [Action("Get file metadata", Description = "Retrieve the metadata for a file in a drive.")]
    public async Task<FileMetadataDto> GetFileMetadataById([ActionParameter] [Display("File ID")] [FileDataSource(typeof(FileDataSourceHandler))] string fileId)
    {
        var request = new RestRequest($"/items/{fileId}", Method.Get);
        return await Client.ExecuteWithHandling<FileMetadataDto>(request);
    }

    [Action("Search files", Description = "Retrieve metadata for files contained in a folder.")]
    public async Task<ListFilesResponse> ListFilesInFolderById(
        [ActionParameter][Display("Folder ID")][FileDataSource(typeof(FolderDataSourceHandler))] string folderId)
    {
        var filesInFolder = new List<FileMetadataDto>();
        string? next = $"/items/{folderId}/children";

        do
        {
            var request = Uri.IsWellFormedUriString(next, UriKind.Absolute)
                ? new RestRequest(new Uri(next!), Method.Get)
                : new RestRequest(next!, Method.Get); 

            var result = await Client.ExecuteWithHandling<ListWrapper<FileMetadataDto>>(request);

            var page = result?.Value ?? Array.Empty<FileMetadataDto>();
            filesInFolder.AddRange(page.Where(i => !string.IsNullOrEmpty(i.MimeType)));

            next = result?.ODataNextLink;
        }
        while (!string.IsNullOrEmpty(next));

        return new ListFilesResponse { Files = filesInFolder };
    }

    [BlueprintActionDefinition(BlueprintAction.DownloadFile)]
    [Action("Download file", Description = "Download a file in a drive.")]
    public async Task<DownloadFileResponse> DownloadFileById([ActionParameter] DownloadFileRequest input)
    {
        var request = new RestRequest($"/items/{input.FileId}/content", Method.Get);
        var response = await Client.ExecuteWithHandling(request);
        
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

    [BlueprintActionDefinition(BlueprintAction.UploadFile)]
    [Action("Upload file", Description = "Upload a file to a parent folder.")]
    public async Task<FileMetadataDto> UploadFileInFolderById(
        [ActionParameter] [Display("Folder ID")] [FileDataSource(typeof(FolderDataSourceHandler))] string? parentFolderId,
        [ActionParameter] UploadFileRequest input)
    {
        const int fourMegabytesInBytes = 4194304;
        var file = await _fileManagementClient.DownloadAsync(input.File);
    
        var fileStream = new MemoryStream();
        await file.CopyToAsync(fileStream);
        fileStream.Position = 0;

        var fileSize = fileStream.Length;
        var contentType = Path.GetExtension(input.File.Name) == ".txt"
            ? MediaTypeNames.Text.Plain
            : input.File.ContentType;
        var fileMetadata = new FileMetadataDto();

        var conflictBehaviour = input.ConflictBehavior ?? "replace";
        parentFolderId = string.IsNullOrWhiteSpace(parentFolderId) ? "root" : parentFolderId;

        if (fileSize < fourMegabytesInBytes)
        {
            var uploadRequest = new RestRequest($".//items/{parentFolderId}:/{input.File.Name}:/content" +
                                                             $"?@microsoft.graph.conflictBehavior={conflictBehaviour}", Method.Put);

            uploadRequest.AddParameter("application/octet-stream", await fileStream.GetByteData(), ParameterType.RequestBody);
            fileMetadata = await Client.ExecuteWithHandling<FileMetadataDto>(uploadRequest);
        }
        else
        {
            const int chunkSize = 3932160;

            var createUploadSessionRequest = new RestRequest(
                $".//items/{parentFolderId}:/{input.File.Name}:/createUploadSession", Method.Post);
            createUploadSessionRequest.AddJsonBody($@"
                {{
                    ""deferCommit"": false,
                    ""item"": {{
                        ""@microsoft.graph.conflictBehavior"": ""{conflictBehaviour}"",
                        ""name"": ""{input.File.Name}""
                    }}
                }}");

            var resumableUploadResult = await Client.ExecuteWithHandling<ResumableUploadDto>(createUploadSessionRequest);
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
    public async Task DeleteFileId([ActionParameter] [Display("File ID")] [FileDataSource(typeof(FileDataSourceHandler))] string fileId)
    {
        var request = new RestRequest($"/items/{fileId}", Method.Delete); 
        await Client.ExecuteWithHandling(request);
    }

    [Action("[Debug] Action", Description = "Debug action")]
    public List<AuthenticationCredentialsProvider> DebugAction() => InvocationContext.AuthenticationCredentialsProviders.ToList();
}