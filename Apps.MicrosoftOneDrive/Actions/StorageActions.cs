﻿using System.Net.Mime;
using Apps.MicrosoftOneDrive.DataSourceHandlers;
using Apps.MicrosoftOneDrive.Dtos;
using Apps.MicrosoftOneDrive.Extensions;
using Apps.MicrosoftOneDrive.Models.Requests;
using Apps.MicrosoftOneDrive.Models.Responses;
using Blackbird.Applications.Sdk.Common;
using Blackbird.Applications.Sdk.Common.Actions;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Dynamic;
using RestSharp;
using File = Blackbird.Applications.Sdk.Common.Files.File;

namespace Apps.MicrosoftOneDrive.Actions;

[ActionList]
public class StorageActions
{
    #region File actions

    [Action("Get file metadata", Description = "Retrieve the metadata for a file in a drive.")]
    public async Task<FileMetadataDto> GetFileMetadataById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("File")] [DataSource(typeof(FileDataSourceHandler))] string fileId)
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
        [ActionParameter] [Display("File")] [DataSource(typeof(FileDataSourceHandler))] string fileId)
    {
        var client = new MicrosoftOneDriveClient();
        var request = new MicrosoftOneDriveRequest($"/items/{fileId}/content", Method.Get, authenticationCredentialsProviders);
        var response = await client.ExecuteWithHandling(request);

        var fileBytes = response.RawBytes;
        var filenameHeader = response.ContentHeaders.First(h => h.Name == "Content-Disposition");
        var filename = filenameHeader.Value.ToString().Split('"')[1];
        var contentType = response.ContentType == MediaTypeNames.Text.Plain
            ? MediaTypeNames.Text.RichText
            : response.ContentType;

        var file = new File(fileBytes)
        {
            Name = filename,
            ContentType = contentType
        };
        return new DownloadFileResponse { File = file };
    }

    [Action("Upload file to folder", Description = "Upload a file to a parent folder.")]
    public async Task<FileMetadataDto> UploadFileInFolderById(
        IEnumerable<AuthenticationCredentialsProvider> authenticationCredentialsProviders,
        [ActionParameter] [Display("Parent folder")] [DataSource(typeof(FolderDataSourceHandler))] string parentFolderId,
        [ActionParameter] UploadFileRequest input)
    {
        const int fourMegabytesInBytes = 4194304;
        var client = new MicrosoftOneDriveClient();
        var fileSize = input.File.Bytes.Length;
        var contentType = Path.GetExtension(input.File.Name) == ".txt"
            ? MediaTypeNames.Text.Plain
            : input.File.ContentType;
        var fileMetadata = new FileMetadataDto();

        if (fileSize < fourMegabytesInBytes)
        {
            var uploadRequest = new MicrosoftOneDriveRequest($".//items/{parentFolderId}:/{input.File.Name}:/content" +
                                                             $"?@microsoft.graph.conflictBehavior={input.ConflictBehavior}",
                Method.Put, authenticationCredentialsProviders);
            uploadRequest.AddParameter(contentType, input.File.Bytes, ParameterType.RequestBody);
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
            
            do
            {
                var startByte = int.Parse(resumableUploadResult.NextExpectedRanges.First().Split("-")[0]);
                var buffer = input.File.Bytes.Skip(startByte).Take(chunkSize).ToArray();
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
                    throw new Exception($"{error.Error.Code}: {error.Error.Message}");
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
        [ActionParameter] [Display("File")] [DataSource(typeof(FileDataSourceHandler))] string fileId)
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
    #endregion
}