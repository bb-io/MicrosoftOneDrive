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
        var fileMetadata = await GetFileMetadataById(input.FileId);
        if (fileMetadata == null)
            throw new PluginApplicationException("File not found or inaccessible.");
        
        var privateUrl = "https://graph.microsoft.com/v1.0/me/drive/items/" + input.FileId + "/content";
        var fileRequest = new HttpRequestMessage(HttpMethod.Get, privateUrl);
        var accessToken = Creds.First(p => p.KeyName == "Authorization").Value;
        fileRequest.Headers.Add("Authorization", accessToken);
        var reference = new FileReference(fileRequest, fileMetadata.Name, MimeTypes.GetMimeType(fileMetadata.Name));
        
        return new DownloadFileResponse { File = reference };
    }

    [Action("Download all files in folder", Description = "Download all files contained in a folder (optionally including subfolders).")]
    public async Task<DownloadAllFilesResponse> DownloadAllFilesInFolder(
    [ActionParameter] DownloadAllFilesInFolderRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.FolderId))
            throw new PluginMisconfigurationException("Folder ID is required.");

        var accessToken = Creds.First(p => p.KeyName == "Authorization").Value;

        var allFileMetas = await GetAllFilesFromFolder(input.FolderId, input.Recursive ?? false);

        var fileReferences = allFileMetas.Select(m =>
        {
            var privateUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/{m.FileId}/content";
            var fileRequest = new HttpRequestMessage(HttpMethod.Get, privateUrl);
            fileRequest.Headers.Add("Authorization", accessToken);

            var name = string.IsNullOrWhiteSpace(m.Name) ? $"{m.FileId}" : m.Name;
            var mime = MimeTypes.GetMimeType(name);

            return new FileReference(fileRequest, name, mime);
        }).ToList();

        return new DownloadAllFilesResponse { Files = fileReferences };
    }


    [BlueprintActionDefinition(BlueprintAction.UploadFile)]
    [Action("Upload file", Description = "Upload a file to a parent folder.")]
    public async Task<FileMetadataDto> UploadFileInFolderById(
        [ActionParameter] [Display("Folder ID")] [FileDataSource(typeof(FolderDataSourceHandler))] string? parentFolderId,
        [ActionParameter] UploadFileRequest input)
    {
        const int fourMegabytesInBytes = 4194304;

        await using var file = await _fileManagementClient.DownloadAsync(input.File);

        var normalizedParentFolderId = NormalizeParentFolderId(parentFolderId);
        var conflictBehaviour = input.ConflictBehavior ?? "replace";
        var contentType = GetContentType(input.File);
        var fileSize = input.File.Size;

        return fileSize < fourMegabytesInBytes
            ? await UploadSmallFile(file, input.File.Name, normalizedParentFolderId, conflictBehaviour)
            : await UploadLargeFile(file, input.File.Name, fileSize, contentType, normalizedParentFolderId, conflictBehaviour);
    }
    
    [Action("Delete file", Description = "Delete file in a drive.")]
    public async Task DeleteFileId([ActionParameter] [Display("File ID")] [FileDataSource(typeof(FileDataSourceHandler))] string fileId)
    {
        var request = new RestRequest($"/items/{fileId}", Method.Delete); 
        await Client.ExecuteWithHandling(request);
    }

    [Action("[Debug] Action", Description = "Debug action")]
    public List<AuthenticationCredentialsProvider> DebugAction() => InvocationContext.AuthenticationCredentialsProviders.ToList();
    
    private static string NormalizeParentFolderId(string? parentFolderId)
    {
        return string.IsNullOrWhiteSpace(parentFolderId) ? "root" : parentFolderId;
    }
    
    private async Task<List<FileMetadataDto>> GetAllFilesFromFolder(string folderId, bool recursive)
    {
        var result = new List<FileMetadataDto>();

        string? next = $"/items/{folderId}/children";
        do
        {
            var request = Uri.IsWellFormedUriString(next, UriKind.Absolute)
                ? new RestRequest(new Uri(next!), Method.Get)
                : new RestRequest(next!, Method.Get);

            var pageResult = await Client.ExecuteWithHandling<ListWrapper<FileMetadataDto>>(request);

            var page = pageResult?.Value ?? Array.Empty<FileMetadataDto>();

            var files = page.Where(i => !string.IsNullOrEmpty(i.MimeType)).ToList();
            result.AddRange(files);

            if (recursive)
            {
                var folders = page.Where(i => string.IsNullOrEmpty(i.MimeType)).ToList();

                foreach (var folder in folders)
                {
                    if (string.IsNullOrWhiteSpace(folder?.FileId)) 
                        continue;
                    
                    var nestedFiles = await GetAllFilesFromFolder(folder.FileId, true);
                    result.AddRange(nestedFiles);
                }
            }

            next = pageResult?.ODataNextLink;
        }
        while (!string.IsNullOrEmpty(next));

        return result;
    }
    
    
    private static string GetContentType(FileReference file)
    {
        return Path.GetExtension(file.Name) == ".txt"
            ? MediaTypeNames.Text.Plain
            : file.ContentType;
    }

    private async Task<FileMetadataDto> UploadSmallFile(
        Stream file,
        string fileName,
        string parentFolderId,
        string conflictBehaviour)
    {
        var uploadRequest = new RestRequest($".//items/{parentFolderId}:/{fileName}:/content" +
                                            $"?@microsoft.graph.conflictBehavior={conflictBehaviour}", Method.Put);

        uploadRequest.AddParameter("application/octet-stream", await file.GetByteData(), ParameterType.RequestBody);

        return await Client.ExecuteWithHandling<FileMetadataDto>(uploadRequest);
    }

    private async Task<FileMetadataDto> UploadLargeFile(
        Stream file,
        string fileName,
        long fileSize,
        string contentType,
        string parentFolderId,
        string conflictBehaviour)
    {
        const int chunkSize = 3932160;

        var resumableUploadResult = await CreateUploadSession(fileName, parentFolderId, conflictBehaviour);
        using var uploadClient = CreateUploadClient(resumableUploadResult.UploadUrl, out var endpoint);

        long uploadedBytes = 0;
        var fileMetadata = new FileMetadataDto();

        do
        {
            EnsureExpectedUploadRange(resumableUploadResult, uploadedBytes);

            var bufferSize = (int)Math.Min(chunkSize, fileSize - uploadedBytes);
            var buffer = await ReadChunk(file, bufferSize);

            var uploadResponseContent = await UploadChunk(
                uploadClient,
                endpoint,
                buffer,
                contentType,
                uploadedBytes,
                fileSize);

            uploadedBytes += buffer.Length;

            resumableUploadResult =
                uploadResponseContent.DeserializeResponseContent<ResumableUploadDto>();

            if (resumableUploadResult.NextExpectedRanges == null)
                fileMetadata = uploadResponseContent.DeserializeResponseContent<FileMetadataDto>();

        } while (resumableUploadResult.NextExpectedRanges != null);

        return fileMetadata;
    }

    private async Task<ResumableUploadDto> CreateUploadSession(
        string fileName,
        string parentFolderId,
        string conflictBehaviour)
    {
        var createUploadSessionRequest = new RestRequest(
            $".//items/{parentFolderId}:/{fileName}:/createUploadSession", Method.Post);

        createUploadSessionRequest.AddJsonBody($@"
            {{
                ""deferCommit"": false,
                ""item"": {{
                    ""@microsoft.graph.conflictBehavior"": ""{conflictBehaviour}"",
                    ""name"": ""{fileName}""
                }}
            }}");

        return await Client.ExecuteWithHandling<ResumableUploadDto>(createUploadSessionRequest);
    }

    private static RestClient CreateUploadClient(string uploadUrl, out string endpoint)
    {
        var uri = new Uri(uploadUrl);
        var baseUrl = uri.GetLeftPart(UriPartial.Authority);
        endpoint = uri.PathAndQuery;

        return new RestClient(new RestClientOptions { BaseUrl = new(baseUrl) });
    }

    private static void EnsureExpectedUploadRange(ResumableUploadDto resumableUploadResult, long uploadedBytes)
    {
        var expectedStartByte = GetNextExpectedStartByte(resumableUploadResult);
        if (expectedStartByte != uploadedBytes)
            throw new PluginApplicationException("Unexpected upload range returned. Cannot continue streaming upload.");
    }

    private static long GetNextExpectedStartByte(ResumableUploadDto resumableUploadResult)
    {
        var nextExpectedRange = resumableUploadResult.NextExpectedRanges?.FirstOrDefault();
        return string.IsNullOrWhiteSpace(nextExpectedRange)
            ? throw new PluginApplicationException("The next expected upload range is null or empty.")
            : long.Parse(nextExpectedRange.Split("-")[0]);
    }

    private static async Task<byte[]> ReadChunk(Stream file, int bufferSize)
    {
        var buffer = new byte[bufferSize];

        var totalBytesRead = 0;
        while (totalBytesRead < bufferSize)
        {
            var bytesRead = await file.ReadAsync(buffer.AsMemory(totalBytesRead, bufferSize - totalBytesRead));
            if (bytesRead == 0)
                break;

            totalBytesRead += bytesRead;
        }

        if (totalBytesRead == 0)
            throw new PluginApplicationException("Unexpected end of file stream during upload.");

        if (totalBytesRead != bufferSize)
            Array.Resize(ref buffer, totalBytesRead);

        return buffer;
    }

    private static async Task<string?> UploadChunk(
        RestClient uploadClient,
        string endpoint,
        byte[] buffer,
        string contentType,
        long startByte,
        long fileSize)
    {
        var uploadRequest = new RestRequest(endpoint, Method.Put);
        uploadRequest.AddParameter(contentType, buffer, ParameterType.RequestBody);
        uploadRequest.AddHeader("Content-Length", buffer.Length);
        uploadRequest.AddHeader("Content-Range", $"bytes {startByte}-{startByte + buffer.Length - 1}/{fileSize}");

        var uploadResponse = await uploadClient.ExecuteAsync(uploadRequest);
        var responseContent = uploadResponse.Content;

        if (uploadResponse.IsSuccessful) 
            return responseContent;
        
        var error = responseContent.DeserializeResponseContent<ErrorDto>();
        throw new PluginApplicationException(error.Error.Message);
    }
}